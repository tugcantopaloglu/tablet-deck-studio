# QR decoder dependency

ZXing core 3.5.3 is bundled locally for offline pairing-code scans.

- Project: https://github.com/zxing/zxing
- Original artifact: https://repo.maven.apache.org/maven2/com/google/zxing/core/3.5.3/core-3.5.3.jar
- SHA-256: `8d8064c1636fdaef7189dd9055c7d59950a8940a12f2293956446ec3c109fd82`
- License: Apache-2.0, reproduced in `LICENSE-ZXING.txt` and the release license folder.

The APK build verifies the pinned digest before compilation. Scans are processed locally; camera frames are never sent to the companion or a third party. Camera permission is requested only when the user opens the scanner; manual pairing remains available.
