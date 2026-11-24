"""Live, non-delivery business evaluation. Uses local API and credentials from environment.

Run with BIZFLOW_EVAL_IDENTIFIER, BIZFLOW_EVAL_PASSWORD and BIZFLOW_EVAL_TENANT.
No approval is sent: every action preview is cancelled. Reads obey the signed-in user's scope.
Reports contain checks and timings, never credentials, tokens, names or business contents.
"""
import json
import os
import sys
import time
import uuid
import traceback
import urllib.request
from pathlib import Path

BASE = os.environ.get("BIZFLOW_EVAL_URL", "http://localhost:5154").rstrip("/")
TOKEN = None
REPORT = []


def post(path, payload):
    headers = {"Content-Type": "application/json"}
    if TOKEN:
        headers["Authorization"] = "Bearer " + TOKEN
    req = urllib.request.Request(BASE + path, data=json.dumps(payload).encode(), headers=headers)
    with urllib.request.urlopen(req, timeout=180) as response:
        return json.load(response)


class Conversation:
    def __init__(self):
        self.id = "business-eval-" + uuid.uuid4().hex
        self.history = []
        self.pending = False

    def chat(self, message):
        result = post("/api/v1/ai/chat", {"message": message, "conversationId": self.id,
                      "locale": "vi", "history": self.history[-20:]})
        self.history += [{"sender": "user", "text": message}, {"sender": "robot", "text": result["response"]}]
        assert result["recognizedCommand"] not in ("AGENT_UNAVAILABLE", "AGENT_STEP_LIMIT"), result["recognizedCommand"]
        assert result["recognizedCommand"] != "EXECUTE_ACTION", "Evaluation must never execute writes"
        if result.get("actionCards"):
            self.pending = True
        return result

    def cancel(self):
        if self.pending:
            result = self.chat("Hủy công việc này, không thực hiện.")
            assert result["recognizedCommand"] == "AGENT_CANCELLED"
            self.pending = False


def preview(response):
    assert response["recognizedCommand"] == "PROPOSE_ACTION", response["recognizedCommand"]
    card = response["actionCards"][0]
    assert card["actionType"] == "CONFIRM_ACTION"
    return card["payload"]["toolName"], json.loads(card["payload"]["argsJson"])


def meeting():
    c = Conversation()
    try:
        first = c.chat("Gửi thông báo tới các phòng ban yêu cầu chuẩn bị cho cuộc họp ngày mai")
        assert first["recognizedCommand"] == "AGENT_CLARIFICATION_REQUIRED"
        second = c.chat("Nội dung về báo cáo tổng kết công ty Quý 4, toàn thể công ty")
        assert second["recognizedCommand"] == "AGENT_CLARIFICATION_REQUIRED"
        assert "Quý 4" in second["response"]
        name, args = preview(c.chat("9h sáng ngày mai tại phòng họp tầng 3, chuẩn bị báo cáo tiến độ Quý 4"))
        assert name == "send_meeting_notification"
        assert args["recipientScope"] == "ALL_COMPANY" and "09:00" in args["startsAt"]
        assert "tầng 3" in args["location"] and "4" in args["topic"]
        name, args = preview(c.chat("Đổi sang 10h30 sáng, địa điểm phòng họp tầng 5; giữ nguyên các nội dung khác"))
        assert "10:30" in args["startsAt"] and "tầng 5" in args["location"]
        assert args["recipientScope"] == "ALL_COMPANY" and "4" in args["topic"]
    finally:
        c.cancel()


def announcement():
    c = Conversation()
    try:
        name, args = preview(c.chat("Gửi thông báo trong ứng dụng cho toàn công ty: Hệ thống chấm công đang bảo trì. Tạm ghi nhận thời gian làm việc vào bảng cá nhân."))
        assert name == "send_department_notification" and args["recipientScope"] == "ALL_COMPANY"
        assert "chấm công" in args["content"].lower()
        name, args = preview(c.chat("Sửa nội dung thành: Hệ thống đã hoạt động trở lại. Mọi người cập nhật các lượt chấm công bị thiếu."))
        assert "hoạt động trở lại" in args["content"]
    finally:
        c.cancel()


def task_draft():
    c = Conversation()
    try:
        name, args = preview(c.chat("Tạo một công việc nháp tên Rà soát chứng từ vận tải, ưu tiên cao, mô tả kiểm tra hóa đơn và biên bản giao nhận."))
        assert name == "create_task_draft" and args["priority"] == "HIGH"
        assert "chứng từ" in args["title"].lower()
    finally:
        c.cancel()


def read_only(message):
    c = Conversation()
    try:
        result = c.chat(message)
        assert result["recognizedCommand"] == "AGENT_REPLY", result["recognizedCommand"]
        assert not result.get("actionCards"), "Read-only requests must not propose writes"
    finally:
        c.cancel()


if __name__ == "__main__":
    login = post("/api/v1/auth/login", {"identifier": os.environ["BIZFLOW_EVAL_IDENTIFIER"],
                 "password": os.environ["BIZFLOW_EVAL_PASSWORD"], "tenantKey": os.environ["BIZFLOW_EVAL_TENANT"]})
    TOKEN = login["accessToken"]
    checks = [
        ("meeting_followups_and_correction", meeting),
        ("business_announcement_and_revision", announcement),
        ("unseen_task_draft", task_draft),
        ("prioritize_actual_accessible_work", lambda: read_only("Kiểm tra công việc hiện có, đề xuất tối đa ba việc nên ưu tiên và giải thích dựa trên dữ liệu. Không tạo hoặc sửa công việc.")),
        ("lookup_people_without_internal_ids", lambda: read_only("Tra cứu nhân viên tên Nguyễn trong phạm vi tôi được phép xem; nếu trùng tên hãy liệt kê lựa chọn. Chưa giao việc.")),
        ("lookup_business_requests", lambda: read_only("Kiểm tra các yêu cầu nghiệp vụ hiện có và tóm tắt tối đa ba yêu cầu từ dữ liệu bạn tra được. Không điều phối.")),
        ("unsupported_email_and_scheduling", lambda: read_only("Bạn có thể hẹn 8 giờ ngày mai tự gửi email báo cáo doanh thu cho khách hàng không? Chỉ giải thích khả năng hiện tại, chưa thực hiện.")),
        ("advisory_without_unrequested_writes", lambda: read_only("Tôi là giám đốc vận hành, muốn giảm tình trạng giao việc không rõ người chịu trách nhiệm. Đề xuất cách cải tiến quy trình ngắn gọn, chưa tạo dữ liệu.")),
    ]
    for name, check in checks:
        started = time.monotonic()
        try:
            check()
            row = {"check": name, "passed": True}
        except Exception as error:
            row = {"check": name, "passed": False, "errorType": type(error).__name__, "reason": str(error)[:180], "line": traceback.extract_tb(error.__traceback__)[-1].lineno}
        row["seconds"] = round(time.monotonic() - started, 2)
        REPORT.append(row)
        print(json.dumps(row), flush=True)
    destination = Path(os.environ.get("BIZFLOW_EVAL_REPORT", "artifacts/agent-business-evaluation.json"))
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps({"timestampUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime()),
                                     "writesConfirmed": 0, "checks": REPORT}, indent=2), encoding="utf-8")
    sys.exit(0 if all(row["passed"] for row in REPORT) else 1)
