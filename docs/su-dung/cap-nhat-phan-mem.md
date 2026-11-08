---
description: Cài bản mới của phần mềm bằng một nút bấm khi phần mềm báo có bản mới.
---

# Cập nhật phần mềm

**Ai làm:** người dùng phần mềm · **Khi nào:** khi thanh **Có bản mới …** hiện ở đầu cửa sổ

Mỗi lần mở, và cứ 6 tiếng một lần nếu để mở cả ngày, phần mềm tự kiểm tra bản mới. Có bản mới thì thanh này hiện ra:

<figure><img src="../.gitbook/assets/thanh-co-ban-moi.png" alt="Thanh Có bản mới 1.0.1 với dòng Có gì mới và hai nút Xem thay đổi, Cập nhật ngay"><figcaption><p>Thông báo có bản mới</p></figcaption></figure>

1. (Không bắt buộc) Bấm **Xem thay đổi** để đọc bản mới có gì. Trang phát hành mở trong trình duyệt.
2. Bấm **Cập nhật ngay**. Thanh đổi thành **Đang tải bản mới … · 45%**.
3. Chờ khoảng 1 phút. Tải xong, phần mềm tự đóng, cài bản mới rồi tự mở lại. Bạn không cần bấm gì trong lúc này.

{% hint style="success" %}
**Kết quả:** phần mềm mở lại với thanh **Đã cập nhật lên bản …**. Bấm **Đóng** để ẩn thanh này. Cài đặt và mã API được giữ nguyên.
{% endhint %}

<figure><img src="../.gitbook/assets/thanh-da-cap-nhat.png" alt="Thanh Đã cập nhật lên bản 1.0.0 với nút Đóng"><figcaption><p>Sau khi cập nhật xong</p></figcaption></figure>

Nếu bạn đang đưa file lên TV, phần mềm nhắc **Đang đưa file lên TV. Đợi xong rồi bấm Cập nhật ngay.** Đợi các file gửi xong rồi bấm lại.

Muốn tự kiểm tra ngay mà không chờ, bấm **Cài đặt** > **Kiểm tra bản mới**. Có bản mới thì nút **Cập nhật ngay** hiện ngay trong cửa sổ **Cài đặt**; bấm nút đó, cửa sổ **Cài đặt** tự đóng và việc tải bản mới hiện ở thanh đầu cửa sổ chính như bước 2.

## Lỗi thường gặp

<details>

<summary>Chưa cập nhật được</summary>

<figure><img src="../.gitbook/assets/thanh-cap-nhat-loi.png" alt="Thanh Chưa cập nhật được với lý do và hai nút Đóng, Thử lại"><figcaption><p>Cập nhật bị lỗi</p></figcaption></figure>

Dòng dưới cho biết lý do, thường là mạng bị ngắt khi đang tải. Kiểm tra mạng rồi bấm **Thử lại**. Nếu thấy **Bản mới tải về bị hỏng hoặc không đúng, phần mềm đã bỏ nó đi.**, phần mềm đã chặn một file không khớp với bản phát hành; bấm **Thử lại** sau ít phút. Lỗi lặp lại thì báo bộ phận IT.

</details>

<details>

<summary>Không kiểm tra được bản mới. Hãy thử lại khi có mạng.</summary>

Dòng này chỉ hiện khi bạn bấm **Kiểm tra bản mới** trong **Cài đặt** lúc mất mạng. Phần mềm vẫn dùng bình thường; lần mở sau nó sẽ tự kiểm tra lại.

</details>
