use std::env;
use std::path::PathBuf;
use std::fs;

fn main() {
    // Get the directory where this build script is located
    let manifest_dir = env::var("CARGO_MANIFEST_DIR").unwrap();
    let lib_path = PathBuf::from(&manifest_dir);
    
    // Determine the library name and extension based on the target platform
    let (lib_name, _lib_ext, link_name) = if cfg!(target_os = "windows") {
        ("HPD-Agent.dll", "dll", "HPD-Agent")
    } else if cfg!(target_os = "macos") {
        ("HPD-Agent.dylib", "dylib", "hpdagent")
    } else {
        // Linux and other Unix-like systems
        ("libHPD-Agent.so", "so", "HPD-Agent")
    };
    
    println!("cargo:rustc-link-search={}", lib_path.display());
    
    // Use different linking strategies per platform
    if cfg!(target_os = "windows") {
        println!("cargo:rustc-link-lib=dylib={}", link_name);
    } else {
        println!("cargo:rustc-link-lib=dylib={}", link_name);
    }
    
    // Copy the library to the target directory for tests
    let target_dir = if let Ok(target) = env::var("CARGO_TARGET_DIR") {
        PathBuf::from(target)
    } else {
        PathBuf::from(&manifest_dir).join("target")
    };
    
    let deps_dir = target_dir.join("debug").join("deps");
    if let Ok(_) = fs::create_dir_all(&deps_dir) {
        let src = lib_path.join(lib_name);
        let dst = deps_dir.join(lib_name);
        let _ = fs::copy(&src, &dst);
        
        // On Unix systems, also create the lib-prefixed symlink if needed
        if !cfg!(target_os = "windows") && !lib_name.starts_with("lib") {
            let unix_name = format!("lib{}", lib_name);
            let unix_link = lib_path.join(&unix_name);
            let unix_dst = deps_dir.join(&unix_name);
            
            // Create symlink in source directory if it doesn't exist
            if !unix_link.exists() {
                #[cfg(unix)]
                {
                    use std::os::unix::fs::symlink;
                    let _ = symlink(lib_name, &unix_link);
                }
            }
            let _ = fs::copy(&unix_link, &unix_dst);
        }
    }
    
    // Inform cargo about the library file so it rebuilds when it changes
    println!("cargo:rerun-if-changed={}", lib_path.join(lib_name).display());
}
