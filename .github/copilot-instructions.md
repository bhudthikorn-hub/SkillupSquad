# Copilot Instructions

## Project Guidelines
- เว็บไซต์ SkillUp ควรใช้ฟอนต์ Noto Sans Thai เป็นฟอนต์หลักทั่วทั้งระบบ

## General Guidelines
- ผู้ใช้อนุญาตให้ดำเนินการแก้ไขโค้ดต่อได้เลยโดยไม่ต้องถามยืนยันซ้ำ เมื่อรายละเอียดสามารถอนุมานได้จากคำขอและบริบท

## Profile Page Guidelines
- สำหรับหน้า Profile ให้แก้เฉพาะ Profile.cshtml โดยใช้ Dashboard styling:
  - หัวข้อ position static margin-bottom 20px; display: block
  - แถวบน 2 คอลัมน์เท่ากัน
  - avatar ใช้ตัวอักษรย่อสีขาวบน gradient indigo
  - skills เป็น flex-wrap pill
  - progress cards เป็นกริด 2 คอลัมน์; ไม่แสดงชื่อคอร์สซ้ำและลิงก์มี padding-bottom 16px
  - achievements ใช้กริด 3 คอลัมน์บนจอใหญ่
  - การ์ด Dark Mode ใช้พื้นหลัง #1e293b ขอบ #334155 และข้อความ #f8fafc
  - รองรับ body.dark-mode