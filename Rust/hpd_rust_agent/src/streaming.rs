// In src/streaming.rs
use tokio::sync::mpsc;
use std::collections::HashMap;
use std::sync::{Mutex, Arc};
use once_cell::sync::Lazy;

// A global, thread-safe map to hold senders for active streams.
static STREAM_SENDERS: Lazy<Arc<Mutex<HashMap<usize, mpsc::UnboundedSender<String>>>>> =
    Lazy::new(|| Arc::new(Mutex::new(HashMap::new())));

#[no_mangle]
pub extern "C" fn stream_callback(context: *mut std::ffi::c_void, event_json_ptr: *const libc::c_char) {
    let context_key = context as usize;

    // A null pointer signals the end of the stream.
    if event_json_ptr.is_null() {
        STREAM_SENDERS.lock().unwrap().remove(&context_key);
        return;
    }

    let c_str = unsafe { std::ffi::CStr::from_ptr(event_json_ptr) };
    let event_json = c_str.to_str().unwrap().to_owned();

    if let Some(sender) = STREAM_SENDERS.lock().unwrap().get(&context_key) {
        // If sending fails, it means the receiver was dropped, so we clean up.
        if sender.send(event_json).is_err() {
            STREAM_SENDERS.lock().unwrap().remove(&context_key);
        }
    }
}

pub fn create_stream() -> (usize, mpsc::UnboundedReceiver<String>) {
    let (tx, rx) = mpsc::unbounded_channel();
    // Use a simple counter for context key instead of casting the sender
    static CONTEXT_COUNTER: std::sync::atomic::AtomicUsize = std::sync::atomic::AtomicUsize::new(1);
    let context_key = CONTEXT_COUNTER.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
    STREAM_SENDERS.lock().unwrap().insert(context_key, tx);
    (context_key, rx)
}
