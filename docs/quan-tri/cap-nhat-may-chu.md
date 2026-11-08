---
description: Đưa bản mới của Worker và trang trình chiếu lên Cloudflare, kiểm tra 2 TV nhận bản mới, và quay về bản cũ nếu cần.
---

# Cập nhật máy chủ và trang chiếu

**Ai làm:** bộ phận IT, người có tài khoản Cloudflare · **Khi nào:** khi có bản mới của thư mục `worker` · **Mất khoảng:** 15 phút

Bản 2 của máy chủ (đi cùng phần mềm 1.0.3) thêm ba việc:

* **TV mở được trang chiếu khi chưa có mạng.** TV tự bật lúc 8:00, đôi khi trước khi Wi-Fi kết nối xong, và trình duyệt rơi về trang trống. Trang chiếu bản 2 lưu sẵn một bản trên TV (file `sw.js`), nên vẫn mở và chiếu ảnh đã lưu, có mạng thì tự cập nhật.
* **Trang chiếu tự phục hồi.** Thử lại khi mất mạng, bỏ qua file lỗi hoặc video đứng hình quá 20 giây, mỗi giờ tự tải lại để trình duyệt của TV nhả bộ nhớ.
* **Theo dõi TV.** Mỗi phút trang chiếu báo về máy chủ (`POST /heartbeat`). Phần mềm đọc `GET /status` để hiện chấm trạng thái trên thẻ TV và mục **Tín hiệu từ TV** trong **Chẩn đoán**.

{% hint style="info" %}
**Trước khi bắt đầu:** cần máy có Node.js, thư mục `worker` của dự án (không nằm trong kho mã công khai), và tài khoản Cloudflare quản lý Worker `signage`. Phần mềm 1.0.3 vẫn chạy bình thường với máy chủ bản cũ, chỉ chưa có phần theo dõi TV.
{% endhint %}

{% stepper %}
{% step %}
### Đưa Worker mới lên

Mở PowerShell trong thư mục `worker`, rồi chạy:

```powershell
npx wrangler@4 login               # mở trình duyệt để đăng nhập Cloudflare, chỉ lần đầu
npx wrangler@4 deploy
```

Kết quả có dòng `Deployed signage triggers` và địa chỉ `https://signage.vienba.workers.dev`. Mã API (secret `API_KEY`) giữ nguyên, không cần nhập lại.
{% endstep %}

{% step %}
### Đưa trang chiếu và file lưu sẵn lên kho

Trong cùng thư mục, chạy:

```powershell
npx wrangler@4 r2 object put signage-vienba/index.html --file=index.html --content-type="text/html; charset=utf-8" --remote
npx wrangler@4 r2 object put signage-vienba/sw.js --file=sw.js --content-type="text/javascript; charset=utf-8" --remote
```

Mỗi lệnh kết thúc bằng `Upload complete`.
{% endstep %}

{% step %}
### Kiểm tra

1. Mở `https://signage.vienba.workers.dev/status` trong trình duyệt. Thấy `{"now":…,"screens":{…}}` là Worker mới đã chạy.
2. Tắt rồi bật từng TV bằng remote để TV mở trang chiếu bản mới.
3. Sau 1–2 phút, mở phần mềm, bấm **Chẩn đoán**. Mục **Tín hiệu từ TV** phải báo cả hai TV **đang chiếu**. Thẻ **TV 1**, **TV 2** có chấm xanh.
{% endstep %}
{% endstepper %}

{% hint style="success" %}
**Kết quả:** sáng hôm sau, nếu TV bật lúc 8:00 khi chưa có Wi-Fi, trang chiếu vẫn mở. Nhật ký của từng TV (mở trang lúc nào, có mạng hay không, file nào bị bỏ qua) nằm trong kết quả **Chẩn đoán** > **Chép kết quả**.
{% endhint %}

## Quay về bản cũ

Bản trước khi cập nhật được giữ trong `worker\previous` (`worker-v1.js`, `index-v1.html`). Chép `worker-v1.js` vào `src\worker.js` rồi chạy lại bước 1; đưa `index-v1.html` lên làm `index.html` như bước 2. File `sw.js` để lại cũng không ảnh hưởng.

## Lỗi thường gặp

<details>

<summary>Mở /status thấy Not found</summary>

**Nguyên nhân:** Worker trên Cloudflare vẫn là bản cũ.

**Cách khắc phục:** chạy lại bước 1 và xem lỗi `wrangler` báo ra. Thường gặp nhất là chưa đăng nhập đúng tài khoản Cloudflare (`npx wrangler@4 whoami`).

</details>

<details>

<summary>Chẩn đoán báo một TV chưa gửi tín hiệu lần nào</summary>

**Nguyên nhân:** TV vẫn đang mở trang chiếu bản cũ, hoặc đang ở trang trình duyệt trống.

**Cách khắc phục:** tắt rồi bật TV bằng remote, chờ 2 phút rồi bấm **Chạy lại** trong **Chẩn đoán**.

</details>
