const video = document.getElementById('video');
const canvas = document.getElementById('canvas');
const result = document.getElementById('result');
const status = document.getElementById('status');
let sent = false;

const MODEL_URL = "https://justadudewhohacks.github.io/face-api.js/models";

async function loadModels() {
    await faceapi.nets.tinyFaceDetector.loadFromUri(MODEL_URL);
    status.innerText = "Model đã tải xong.";
    startCamera();
}

async function startCamera() {
    const stream = await navigator.mediaDevices.getUserMedia({ video: true });
    video.srcObject = stream;
    video.onloadedmetadata = () => {
        video.play();
        detectLoop();
    };
}

async function detectLoop() {
    const options = new faceapi.TinyFaceDetectorOptions({ inputSize: 160 });
    const detection = await faceapi.detectSingleFace(video, options);

    if (detection && !sent) {
        status.innerText = "Phát hiện khuôn mặt. Đang gửi...";
        sent = true;

        const ctx = canvas.getContext('2d');
        ctx.drawImage(video, 0, 0, canvas.width, canvas.height);

        canvas.toBlob(async (blob) => {
            const formData = new FormData();
            formData.append("image", blob, "face.jpg");

            try {
                const res = await fetch("/Identity/Account/RegisterFace?handler=Post", {
                    method: "POST",
                    body: formData
                });

                const data = await res.json();
                if (data.success) {
                    result.innerText = "" + data.message;
                    status.innerText = "Thành công.";

                    window.location.href = "/Identity/Account/RegisterFaceSuccess";

                } else {
                    result.innerText = "" + data.message;
                    status.innerText = "Thử lại.";
                    sent = false;
                }
            } catch (err) {
                result.innerText = "Lỗi: " + err.message;
                sent = false;
            }
        }, 'image/jpeg');
    }

    requestAnimationFrame(detectLoop);
}

window.addEventListener("DOMContentLoaded", loadModels);
