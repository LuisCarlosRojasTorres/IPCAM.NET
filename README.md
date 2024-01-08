IPCAM - Thread to TASKs
Changelog:

List of Changes:
- Change Thread to Task
	- Initialize

TODOs:
- Change to ManualResetEventSlim
- Change Thread to Task
	- Delete all references
- Change arguments of events to Frame to byteArrays
- dELETE EXTRA VARS

# IPCAM.NET
A lite version of AForge for IP Cameras in NETCore6

## What is boundary in IPCameras
In the context of **IP cameras**, the term "**myboundary**" typically refers to a **boundary marker** used in the **MJPEG (Motion JPEG)** video stream format. Let me explain further:

1. **MJPEG (Motion JPEG)**: MJPEG is a video compression format where each frame of the video is individually compressed as a separate JPEG image. Unlike other video formats, MJPEG doesn't rely on inter-frame compression, making it simpler but less efficient in terms of file size.

2. **Multipart/X-Mixed-Replace**:
    - When an IP camera streams video using MJPEG, it often uses the **multipart/x-mixed-replace** content type.
    - This content type allows the camera to send a continuous stream of images (JPEG frames) over a single HTTP connection.
    - The stream is divided into **parts**, each containing an individual image.
    - The **boundary** marker separates these parts, indicating where one image ends and the next begins.

3. **Boundary Marker ("myboundary")**:
    - The boundary marker is a unique string that identifies the start and end of each image within the MJPEG stream.
    - In the HTTP response header, you'll find something like:
        ```
        Content-Type: multipart/x-mixed-replace; boundary=myboundary
        ```
    - Between the boundaries, the camera sends the actual image data (encoded JPEG) along with other relevant information.

4. **Usage**:
    - Clients (such as web browsers or media players) that consume MJPEG streams look for this specific structure.
    - They expect the stream to follow the pattern:
        ```
        --myboundary
        Content-Type: image/jpeg
        Content-Length: [length]
        [image data]
        ```
    - The empty line after the `Content-Length` field is crucial.

5. **Reverse Proxy Consideration**:
    - If you're building a reverse proxy application to display the camera feed, ensure that your proxy correctly handles the boundary markers.
    - Redirecting users directly to the camera's MJPEG stream (e.g., `http://ipcam/mjpg/video.mjpg`) might be a simpler approach.

Remember, this technical detail ensures that the MJPEG stream is correctly interpreted by clients, allowing them to display the video feed seamlessly. 📷🎥

Origen: Conversación con Bing, 21/12/2023
(1) HttpWebResponse with MJPEG and multipart/x-mixed-replace; boundary .... https://stackoverflow.com/questions/2060953/httpwebresponse-with-mjpeg-and-multipart-x-mixed-replace-boundary-myboundary.
(2) IP Camera - Bindings | openHAB. https://www.openhab.org/addons/bindings/ipcamera/.
(3) IP Camera Based Video Surveillance Using Object’s Boundary Specification. https://www.mecs-press.org/ijitcs/ijitcs-v8-n8/IJITCS-V8-N8-2.pdf.
