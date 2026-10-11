// GET /api/orders/export — Xuất Excel danh sách đơn hàng.
import ExcelJS from 'exceljs';
import { handle, auth, audit } from '@/lib/http';
import { listOrders } from '@/lib/b1-client';
import { ApiError } from '@/lib/auth';


export async function GET(req: Request) {
  return handle(async () => {
    const user = await auth(req, 'viewer');
    const url = new URL(req.url);
    const q = url.searchParams;
    const status = q.get('status') || '';
    const cardCode = q.get('card_code') || null;
    const docNumRaw = q.get('doc_num');
    const docNum = docNumRaw ? parseInt(docNumRaw, 10) : null;
    let spc: number | null = null;
    if (user.role === 'sales') {
      if (!user.sales_person_code)
        throw new ApiError(403, 'Tài khoản chưa được gán nhân viên kinh doanh. Liên hệ quản trị viên.');
      spc = user.sales_person_code;
    }
    const { items } = listOrders(status, cardCode, docNum, spc, 1000, 0);

    const wb = new ExcelJS.Workbook();
    const ws = wb.addWorksheet('DonHang');
    const headers = ['Số đơn', 'Ngày', 'Mã KH', 'Tên KH', 'Tổng tiền', 'Trạng thái', 'Ghi chú'];
    const headerRow = ws.addRow(headers);
    headerRow.font = { bold: true, color: { argb: 'FFFFFFFF' } };
    headerRow.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FF1E3A5F' } };
    const statusLabel = (s: string) =>
      s === 'open' ? 'Đang mở' : s === 'closed' ? 'Đã đóng' : s === 'cancelled' ? 'Đã huỷ' : s;
    for (const o of items) {
      const row = ws.addRow([
        o.doc_num,
        o.doc_date,
        o.card_code,
        o.card_name,
        o.doc_total,
        statusLabel(o.status),
        o.comments,
      ]);
      row.getCell(5).numFmt = '#,##0';
    }
    ws.columns.forEach((c) => {
      c.width = 18;
    });

    audit(req, user, 'order.export', 'Orders', `excel:${items.length}`, {
      status,
      count: items.length,
    });

    const buf = await wb.xlsx.writeBuffer();
    const now = new Date();
    const fname = `donhang-${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(
      now.getDate()
    ).padStart(2, '0')}-${String(now.getHours()).padStart(2, '0')}${String(now.getMinutes()).padStart(
      2,
      '0'
    )}${String(now.getSeconds()).padStart(2, '0')}.xlsx`;
    return new Response(buf, {
      headers: {
        'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
        'Content-Disposition': `attachment; filename="${fname}"`,
      },
    });
  });
}
