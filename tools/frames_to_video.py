#!/usr/bin/env python3
"""Turns the files written by the in-game command `fof_capture_killers` into website videos.

Usage:  python frames_to_video.py <folder with the .dat files> <output models folder>
Needs ffmpeg on the PATH. Makes <name>.webm and <name>.mp4 for each <name>.dat (24 frames per second, looping).
"""
import os, shutil, struct, subprocess, sys, tempfile


def read_frames(path):
    d = open(path, "rb").read()
    n = struct.unpack("<I", d[:4])[0]
    o, frames = 4, []
    for _ in range(n):
        (ln,) = struct.unpack("<I", d[o:o + 4])
        o += 4
        frames.append(d[o:o + ln])
        o += ln
    if o != len(d):
        raise ValueError("%s: %d stray bytes" % (path, len(d) - o))
    return frames


def main(src, dst):
    os.makedirs(dst, exist_ok=True)
    bins = sorted(f for f in os.listdir(src) if f.endswith(".dat"))
    if not bins:
        sys.exit("No .dat files in " + src)
    for f in bins:
        name = f[:-4]
        frames = read_frames(os.path.join(src, f))
        tmp = tempfile.mkdtemp()
        try:
            for i, jpg in enumerate(frames):
                open(os.path.join(tmp, "%03d.jpg" % i), "wb").write(jpg)
            inp = ["ffmpeg", "-y", "-loglevel", "error", "-framerate", "24", "-i", os.path.join(tmp, "%03d.jpg")]
            subprocess.check_call(inp + ["-c:v", "libvpx-vp9", "-b:v", "0", "-crf", "34", "-pix_fmt", "yuv420p", "-an",
                                         os.path.join(dst, name + ".webm")])
            subprocess.check_call(inp + ["-c:v", "libx264", "-crf", "26", "-pix_fmt", "yuv420p", "-an",
                                         "-movflags", "+faststart", os.path.join(dst, name + ".mp4")])
        finally:
            shutil.rmtree(tmp, ignore_errors=True)
        print("%s: %d frames -> %s.webm / .mp4" % (name, len(frames), name))


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
