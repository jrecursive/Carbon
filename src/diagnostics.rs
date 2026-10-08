//! Opt-in, bounded diagnostics. All collection and completed snapshot access is
//! synchronized by the caller; this module never reads live profiler TLS data.
use serde::Serialize;
use std::collections::VecDeque;

pub const MAX_EXCEPTION_BUCKETS: usize = 128;
pub const MAX_EXCEPTION_FRAMES: usize = 16;
pub const MAX_GC_EVENTS: usize = 256;
pub const MAX_METADATA_BYTES: usize = 512;
pub const GC_BOUNDS_US: [u64; 8] = [1_000, 5_000, 10_000, 25_000, 50_000, 100_000, 250_000, 500_000];

#[derive(Clone, PartialEq, Eq)]
pub struct ExceptionKey {
    pub class: usize,
    pub thread: u64,
    pub main: bool,
    pub instrumented: usize,
    pub frames: [usize; MAX_EXCEPTION_FRAMES],
    pub frame_count: usize,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ExceptionBucket {
    #[serde(rename = "type")]
    pub exception_type: String,
    pub assembly: String,
    pub thread_kind: &'static str,
    pub thread_fingerprint: String,
    pub instrumented_method: String,
    pub stack_fingerprint: String,
    pub stack: Vec<String>,
    pub count: u64,
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GcEvent {
    pub end_offset_us: u64,
    pub pause_us: u64,
}

#[derive(Default)]
pub struct DiagnosticCounts {
    pub observed: u64,
    pub unattributed: u64,
    pub overflow: u64,
    buckets: Vec<(ExceptionKey, ExceptionBucket)>,
    pub pause_count: u64,
    pub total_pause_us: u64,
    pub max_pause_us: u64,
    pub histogram: [u64; 9],
    events: VecDeque<GcEvent>,
    pub overwritten_events: u64,
}

impl DiagnosticCounts {
    pub fn exception(&mut self, key: ExceptionKey, names: impl FnOnce() -> ExceptionBucket) {
        self.observed = self.observed.saturating_add(1);
        if key.instrumented == 0 { self.unattributed = self.unattributed.saturating_add(1); }
        if let Some((_, bucket)) = self.buckets.iter_mut().find(|(existing, _)| *existing == key) {
            bucket.count = bucket.count.saturating_add(1);
        } else if self.buckets.len() < MAX_EXCEPTION_BUCKETS {
            let mut bucket = names();
            bucket.count = 1;
            bucket.stack.truncate(MAX_EXCEPTION_FRAMES);
            self.buckets.push((key, bucket));
        } else {
            self.overflow = self.overflow.saturating_add(1);
        }
    }

    pub fn gc_pause(&mut self, pause_us: u64, end_offset_us: u64) {
        self.pause_count = self.pause_count.saturating_add(1);
        self.total_pause_us = self.total_pause_us.saturating_add(pause_us);
        self.max_pause_us = self.max_pause_us.max(pause_us);
        let bin = GC_BOUNDS_US.iter().position(|bound| pause_us <= *bound).unwrap_or(GC_BOUNDS_US.len());
        self.histogram[bin] = self.histogram[bin].saturating_add(1);
        if self.events.len() == MAX_GC_EVENTS {
            self.events.pop_front();
            self.overwritten_events = self.overwritten_events.saturating_add(1);
        }
        self.events.push_back(GcEvent { end_offset_us, pause_us });
    }

    pub fn snapshot(&self, duration_seconds: f64) -> Vec<u8> {
        #[derive(Serialize)]
        #[serde(rename_all = "camelCase")]
        struct Exceptions<'a> {
            observed: u64, unattributed: u64, overflow: u64, buckets: Vec<&'a ExceptionBucket>,
        }
        #[derive(Serialize)]
        #[serde(rename_all = "camelCase")]
        struct Gc<'a> {
            interval: &'static str, pause_count: u64, total_pause_us: u64, max_pause_us: u64,
            histogram_upper_bounds_us: &'a [u64], histogram: &'a [u64], events: &'a VecDeque<GcEvent>, overwritten_events: u64,
        }
        #[derive(Serialize)]
        #[serde(rename_all = "camelCase")]
        struct Snapshot<'a> { schema_version: u32, duration_seconds: f64, exceptions: Exceptions<'a>, gc: Gc<'a> }
        serde_json::to_vec(&Snapshot {
            schema_version: 1, duration_seconds,
            exceptions: Exceptions { observed: self.observed, unattributed: self.unattributed, overflow: self.overflow,
                buckets: self.buckets.iter().map(|(_, bucket)| bucket).collect() },
            gc: Gc { interval: "PreStopWorld..PostStartWorldUnlocked", pause_count: self.pause_count,
                total_pause_us: self.total_pause_us, max_pause_us: self.max_pause_us,
                histogram_upper_bounds_us: &GC_BOUNDS_US, histogram: &self.histogram, events: &self.events,
                overwritten_events: self.overwritten_events },
        }).unwrap_or_default()
    }
}

pub fn bounded_metadata(value: &str) -> String {
    let mut end = value.len().min(MAX_METADATA_BYTES);
    while !value.is_char_boundary(end) { end -= 1; }
    value[..end].to_owned()
}

#[cfg(test)]
mod tests {
    use super::*;
    fn key(class: usize, instrumented: usize) -> ExceptionKey {
        ExceptionKey { class, instrumented, thread: 1, main: false, frames: [0; MAX_EXCEPTION_FRAMES], frame_count: 0 }
    }
    fn names() -> ExceptionBucket {
        ExceptionBucket { exception_type: "System.InvalidCastException".into(), assembly: "mscorlib".into(),
            thread_kind: "background", thread_fingerprint: "1".into(), instrumented_method: "".into(),
            stack_fingerprint: "1".into(), stack: vec![], count: 0 }
    }
    #[test]
    fn exceptions_include_unattributed_and_keep_existing_buckets_after_overflow() {
        let mut counts = DiagnosticCounts::default();
        for i in 0..MAX_EXCEPTION_BUCKETS + 4 { counts.exception(key(i, 0), names); }
        counts.exception(key(0, 0), || panic!("existing bucket must not format metadata again"));
        assert_eq!(counts.observed, (MAX_EXCEPTION_BUCKETS + 5) as u64);
        assert_eq!(counts.unattributed, counts.observed);
        assert_eq!(counts.overflow, 4);
        assert_eq!(counts.buckets.len(), MAX_EXCEPTION_BUCKETS);
        assert_eq!(counts.buckets[0].1.count, 2);
        let snapshot: serde_json::Value = serde_json::from_slice(&counts.snapshot(1.5)).unwrap();
        assert_eq!(snapshot["exceptions"]["observed"], counts.observed);
        assert_eq!(snapshot["durationSeconds"], 1.5);
        assert_eq!(snapshot["schemaVersion"], 1);
    }
    #[test]
    fn histogram_edges_and_event_ring_keep_full_totals() {
        let mut counts = DiagnosticCounts::default();
        counts.gc_pause(0, 0);
        for bound in GC_BOUNDS_US { counts.gc_pause(bound, 0); counts.gc_pause(bound + 1, 0); }
        assert_eq!(counts.histogram.iter().sum::<u64>(), 17);
        assert_eq!(counts.histogram[0], 2);
        assert_eq!(counts.histogram[8], 1);
        for i in 0..MAX_GC_EVENTS + 10 { counts.gc_pause(600_000, i as u64); }
        assert_eq!(counts.events.len(), MAX_GC_EVENTS);
        assert_eq!(counts.overwritten_events, 27);
        assert_eq!(counts.pause_count, (MAX_GC_EVENTS + 27) as u64);
        assert_eq!(counts.max_pause_us, 600_000);
        assert_eq!(counts.histogram.iter().sum::<u64>(), counts.pause_count);
        assert_eq!(counts.events.front().unwrap().end_offset_us, 10);
    }
    #[test]
    fn metadata_bound_preserves_utf8() {
        let value = "é".repeat(MAX_METADATA_BYTES);
        let bounded = bounded_metadata(&value);
        assert_eq!(bounded.len(), MAX_METADATA_BYTES);
        assert!(bounded.is_char_boundary(bounded.len()));
    }
}
