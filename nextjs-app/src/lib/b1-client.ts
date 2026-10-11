// lib/b1-client.ts — Giả lập Service Layer trong bộ nhớ (mock mode) + nghiệp vụ đơn hàng.
// Port từ MockB1Client.cs / OrderService.cs sang TypeScript.
import { logSlAudit } from './db';

export class B1Error extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'B1Error';
  }
}

type Rec = Record<string, unknown>;

const str = (o: Rec, k: string): string => {
  const v = o[k];
  return v === null || v === undefined ? '' : String(v);
};
const num = (o: Rec, k: string, dflt = 0): number => {
  const v = o[k];
  if (v === null || v === undefined || v === '') return dflt;
  const n = Number(v);
  return Number.isFinite(n) ? n : dflt;
};
const intN = (o: Rec, k: string): number | null => {
  const v = o[k];
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? Math.trunc(n) : null;
};
const dictList = (o: Rec, k: string): Rec[] => {
  const v = o[k];
  return Array.isArray(v) ? (v as Rec[]) : [];
};
const deepClone = <T>(o: T): T => JSON.parse(JSON.stringify(o));
const todayStr = (): string => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};

const UDF = process.env.EXTERNAL_ID_UDF || 'U_ExternalID';

// Dữ liệu mock cố định (giống MockB1Client.cs)
const MOCK_CUSTOMERS: Rec[] = [
  { CardCode: 'C20000', CardName: 'Công ty TNHH An Phát', Phone1: '0901111111', SalesPersonCode: 1, PriceListNum: 1 },
  { CardCode: 'C20001', CardName: 'Cửa hàng Bình Minh', Phone1: '0902222222', SalesPersonCode: 2, PriceListNum: 1 },
  { CardCode: 'C20002', CardName: 'Siêu thị Coop Home', Phone1: '0903333333', SalesPersonCode: 1, PriceListNum: 2 },
];
const MOCK_ITEMS: Rec[] = [
  { ItemCode: 'A00001', ItemName: 'Bàn làm việc gỗ', ItemType: 'itItems', SalesUnit: 'Cái', Price: 1500000, TaxCode: 'S1' },
  { ItemCode: 'A00002', ItemName: 'Ghế xoay văn phòng', ItemType: 'itItems', SalesUnit: 'Cái', Price: 850000, TaxCode: 'S1' },
  { ItemCode: 'A00003', ItemName: 'Tủ hồ sơ 4 ngăn', ItemType: 'itItems', SalesUnit: 'Cái', Price: 2300000, TaxCode: 'S1' },
];
const MOCK_WAREHOUSES: Rec[] = [
  { WarehouseCode: '01', WarehouseName: 'Kho chính' },
  { WarehouseCode: '02', WarehouseName: 'Kho chi nhánh' },
];
const MOCK_PAYMENT_TERMS: Rec[] = [
  { GroupNumber: 1, PaymentTermsGroupName: 'Thanh toán ngay' },
  { GroupNumber: 2, PaymentTermsGroupName: 'Công nợ 30 ngày' },
  { GroupNumber: 3, PaymentTermsGroupName: 'Công nợ 60 ngày' },
];
const MOCK_SHIPPING_TYPES: Rec[] = [
  { Code: 1, Name: 'Giao hàng tiêu chuẩn' },
  { Code: 2, Name: 'Giao hàng nhanh' },
  { Code: 3, Name: 'Khách tự vận chuyển' },
];
const MOCK_SALES_PERSONS: Rec[] = [
  { SalesEmployeeCode: 1, SalesEmployeeName: 'Nguyễn Văn A' },
  { SalesEmployeeCode: 2, SalesEmployeeName: 'Trần Thị B' },
];
const MOCK_TAX_CODES: Rec[] = [
  { Code: 'S0', Name: 'Không chịu thuế', Rate: 0 },
  { Code: 'S1', Name: 'VAT 8%', Rate: 8 },
  { Code: 'S2', Name: 'VAT 10%', Rate: 10 },
  { Code: 'SV', Name: 'VAT dịch vụ 10%', Rate: 10 },
];

// Singleton mock client (giữ state trong bộ nhớ tiến trình)
class MockB1Client {
  private seq = 1000;
  private orders = new Map<number, Rec>();

  constructor() {
    this.seedOrder();
  }

  private etag(order: Rec): string {
    return `W/"${order['DocEntry']}-${order['_version']}"`;
  }

  private seedOrder() {
    this.seq++;
    const docEntry = this.seq;
    const today = todayStr();
    this.orders.set(docEntry, {
      DocEntry: docEntry,
      DocNum: 5001,
      DocDate: today,
      DocDueDate: today,
      TaxDate: today,
      CardCode: 'C20000',
      CardName: 'Công ty TNHH An Phát',
      NumAtCard: 'PO-2026-001',
      DocTotal: 3000000,
      DocCurrency: 'VND',
      DocRate: 1.0,
      DocumentStatus: 'bost_Open',
      Cancelled: 'tNO',
      Comments: 'Đơn mẫu (mock)',
      SalesPersonCode: 1,
      ContactPersonCode: null,
      ShipToCode: 'Kho nhận hàng',
      PayToCode: 'Trụ sở',
      PaymentGroupCode: 2,
      TransportationCode: 1,
      Project: null,
      Series: 1,
      DiscountPercent: 0,
      Address: '123 Nguyễn Huệ, TP.HCM',
      UpdateDate: today,
      UpdateTime: '0900',
      [UDF]: 'MOCK-001',
      _version: 1,
      DocumentLines: [
        {
          LineNum: 0,
          ItemCode: 'A00001',
          ItemDescription: 'Bàn làm việc gỗ',
          Quantity: 2,
          Price: 1500000,
          WarehouseCode: '01',
          LineStatus: 'bost_Open',
          TaxCode: 'V10',
          DiscountPercent: 0,
          ShipDate: today,
          ProjectCode: null,
          CostingCode: null,
          UoMCode: 'Cái',
        },
      ],
    });
  }

  private checkCustomer(cardCode: string): Rec {
    const c = MOCK_CUSTOMERS.find((x) => str(x, 'CardCode') === cardCode);
    if (!c) throw new B1Error(`B1 lỗi 400: 1320000042 - Business partner '${cardCode}' không tồn tại.`);
    return c;
  }

  createOrder(payload: Rec): Rec {
    const customer = this.checkCustomer(str(payload, 'CardCode'));
    if (dictList(payload, 'DocumentLines').length === 0)
      throw new B1Error('B1 lỗi 400: Đơn hàng phải có ít nhất 1 dòng hàng.');
    this.seq++;
    const docEntry = this.seq;
    const lines: Rec[] = [];
    let total = 0;
    let i = 0;
    const today = todayStr();
    for (const ln of dictList(payload, 'DocumentLines')) {
      const qty = num(ln, 'Quantity');
      const price = ln['Price'] !== null && ln['Price'] !== undefined ? num(ln, 'Price') : num(ln, 'UnitPrice');
      const disc = num(ln, 'DiscountPercent');
      const lineTotal = qty * price * (1 - disc / 100);
      total += lineTotal;
      const item = MOCK_ITEMS.find((x) => str(x, 'ItemCode') === str(ln, 'ItemCode'));
      lines.push({
        LineNum: i++,
        ItemCode: str(ln, 'ItemCode'),
        ItemDescription: item ? str(item, 'ItemName') : str(ln, 'ItemCode'),
        Quantity: qty,
        Price: price,
        DiscountPercent: disc,
        WarehouseCode: str(ln, 'WarehouseCode') || '01',
        TaxCode: str(ln, 'TaxCode') || 'V10',
        ShipDate: str(ln, 'ShipDate') || today,
        ProjectCode: ln['ProjectCode'] ?? null,
        CostingCode: ln['CostingCode'] ?? null,
        UoMCode: ln['UoMCode'] ?? null,
        LineStatus: 'bost_Open',
        LineTotal: lineTotal,
      });
    }
    const headerDisc = num(payload, 'DiscountPercent');
    total *= 1 - headerDisc / 100;
    const docDate = str(payload, 'DocDate') || today;
    const order: Rec = {
      DocEntry: docEntry,
      DocNum: 5000 + (docEntry - 1000),
      DocDate: docDate,
      DocDueDate: str(payload, 'DocDueDate') || today,
      TaxDate: str(payload, 'TaxDate') || docDate,
      CardCode: str(payload, 'CardCode'),
      CardName: str(customer, 'CardName'),
      NumAtCard: payload['NumAtCard'] ?? null,
      DocTotal: Math.round(total * 100) / 100,
      DocCurrency: str(payload, 'DocCurrency') || 'VND',
      DocRate: payload['DocRate'] !== null && payload['DocRate'] !== undefined ? num(payload, 'DocRate') : 1.0,
      DocumentStatus: 'bost_Open',
      Cancelled: 'tNO',
      Comments: str(payload, 'Comments'),
      DiscountPercent: headerDisc,
      SalesPersonCode:
        payload['SalesPersonCode'] !== null && payload['SalesPersonCode'] !== undefined
          ? payload['SalesPersonCode']
          : intN(customer, 'SalesPersonCode'),
      ContactPersonCode: payload['ContactPersonCode'] ?? null,
      ShipToCode: payload['ShipToCode'] ?? null,
      PayToCode: payload['PayToCode'] ?? null,
      PaymentGroupCode: payload['PaymentGroupCode'] ?? null,
      TransportationCode: payload['TransportationCode'] ?? null,
      Project: payload['Project'] ?? null,
      Series: payload['Series'] ?? null,
      UpdateDate: today,
      UpdateTime: '0900',
      [UDF]: payload[UDF] ?? null,
      _version: 1,
      DocumentLines: lines,
    };
    this.orders.set(docEntry, order);
    return deepClone(order);
  }

  getOrder(docEntry: number): { order: Rec; etag: string } {
    const order = this.orders.get(docEntry);
    if (!order) throw new B1Error(`B1 lỗi 404: Không tìm thấy đơn hàng DocEntry=${docEntry}.`);
    return { order: deepClone(order), etag: this.etag(order) };
  }

  listOrders(top = 50, skip = 0, filter?: string): { total: number; value: Rec[] } {
    let orders = [...this.orders.values()];
    if (filter) orders = orders.filter((o) => matchFilter(o, filter));
    const total = orders.length;
    const page = orders
      .sort((a, b) => (intN(b, 'DocEntry') ?? 0) - (intN(a, 'DocEntry') ?? 0))
      .slice(skip, skip + top)
      .map((o) => {
        const c = deepClone(o);
        delete c['DocumentLines'];
        return c;
      });
    return { total, value: page };
  }

  updateOrder(docEntry: number, payload: Rec, etag?: string | null) {
    const order = this.orders.get(docEntry);
    if (!order) throw new B1Error(`B1 lỗi 404: Không tìm thấy đơn hàng DocEntry=${docEntry}.`);
    if (etag && etag !== this.etag(order))
      throw new B1Error('B1 lỗi 412: Đơn hàng đã bị người khác sửa (xung đột ETag). Hãy tải lại.');
    if (str(order, 'Cancelled') === 'tYES') throw new B1Error('B1 lỗi 400: Không thể sửa đơn hàng đã hủy.');
    for (const key of [
      'Comments', 'DocDueDate', 'TaxDate', 'DiscountPercent', 'NumAtCard',
      'ContactPersonCode', 'ShipToCode', 'PayToCode', 'PaymentGroupCode',
      'TransportationCode', 'Project', 'DocCurrency', 'DocRate',
    ])
      if (key in payload) order[key] = payload[key];
    if ('DocumentLines' in payload) {
      const byNum = new Map<number, Rec>();
      for (const l of dictList(order, 'DocumentLines')) {
        const n = intN(l, 'LineNum');
        if (n !== null) byNum.set(n, l);
      }
      for (const pl of dictList(payload, 'DocumentLines')) {
        const lnNum = intN(pl, 'LineNum');
        const ln = lnNum !== null ? byNum.get(lnNum) : undefined;
        if (!ln) continue;
        for (const k of ['Quantity', 'WarehouseCode', 'TaxCode', 'DiscountPercent', 'LineStatus', 'ShipDate', 'ProjectCode', 'CostingCode', 'UoMCode'])
          if (k in pl) ln[k] = pl[k];
        if (pl['Price'] !== null && pl['Price'] !== undefined) ln['Price'] = pl['Price'];
        else if (pl['UnitPrice'] !== null && pl['UnitPrice'] !== undefined) ln['Price'] = pl['UnitPrice'];
      }
      order['DocTotal'] =
        Math.round(
          dictList(order, 'DocumentLines')
            .filter((l) => str(l, 'LineStatus') === 'bost_Open')
            .reduce((s, l) => s + num(l, 'Quantity') * num(l, 'Price') * (1 - num(l, 'DiscountPercent') / 100), 0) * 100
        ) / 100;
    }
    order['_version'] = (intN(order, '_version') ?? 0) + 1;
    order['UpdateDate'] = todayStr();
  }

  cancelOrder(docEntry: number): Rec {
    const order = this.orders.get(docEntry);
    if (!order) throw new B1Error(`B1 lỗi 404: Không tìm thấy đơn hàng DocEntry=${docEntry}.`);
    if (str(order, 'Cancelled') === 'tYES') throw new B1Error('B1 lỗi 400: Đơn hàng đã được hủy trước đó.');
    order['Cancelled'] = 'tYES';
    order['DocumentStatus'] = 'bost_Close';
    order['_version'] = (intN(order, '_version') ?? 0) + 1;
    return deepClone(order);
  }

  closeOrderLines(docEntry: number, lineNums?: number[] | null) {
    const order = this.orders.get(docEntry);
    if (!order) throw new B1Error(`B1 lỗi 404: Không tìm thấy đơn hàng DocEntry=${docEntry}.`);
    for (const ln of dictList(order, 'DocumentLines')) {
      const lnNum = intN(ln, 'LineNum');
      if ((!lineNums || (lnNum !== null && lineNums.includes(lnNum))) && str(ln, 'LineStatus') === 'bost_Open')
        ln['LineStatus'] = 'bost_Close';
    }
    if (dictList(order, 'DocumentLines').every((l) => str(l, 'LineStatus') === 'bost_Close'))
      order['DocumentStatus'] = 'bost_Close';
    order['_version'] = (intN(order, '_version') ?? 0) + 1;
  }

  findOrderByExternalId(externalId: string): Rec | null {
    for (const o of this.orders.values()) {
      if (o[UDF] !== null && o[UDF] !== undefined && String(o[UDF]) === externalId) {
        const c = deepClone(o);
        delete c['DocumentLines'];
        return c;
      }
    }
    return null;
  }

  // ---------------- Master data ----------------
  searchCustomers(q: string, top = 20): Rec[] {
    const ql = q.toLowerCase();
    return MOCK_CUSTOMERS.filter(
      (c) => str(c, 'CardCode').toLowerCase().includes(ql) || str(c, 'CardName').toLowerCase().includes(ql)
    ).slice(0, top);
  }
  searchItems(q: string, top = 20): Rec[] {
    const ql = q.toLowerCase();
    return MOCK_ITEMS.filter(
      (i) => str(i, 'ItemCode').toLowerCase().includes(ql) || str(i, 'ItemName').toLowerCase().includes(ql)
    ).slice(0, top);
  }
  listWarehouses = () => [...MOCK_WAREHOUSES];
  listPaymentTerms = () => [...MOCK_PAYMENT_TERMS];
  listShippingTypes = () => [...MOCK_SHIPPING_TYPES];
  listSalesPersons = () => [...MOCK_SALES_PERSONS];
  fetchCustomersPage = (top = 500, skip = 0) => MOCK_CUSTOMERS.slice(skip, skip + top);
  fetchItemsPage = (top = 500, skip = 0) => MOCK_ITEMS.slice(skip, skip + top);
  fetchTaxCodes = () => [...MOCK_TAX_CODES];
}

function matchFilter(order: Rec, filt: string): boolean {
  let ok = true;
  if (filt.includes("DocumentStatus eq 'bost_Open'"))
    ok &&= str(order, 'DocumentStatus') === 'bost_Open' && str(order, 'Cancelled') === 'tNO';
  if (filt.includes("DocumentStatus eq 'bost_Close'")) ok &&= str(order, 'DocumentStatus') === 'bost_Close';
  if (filt.includes("Cancelled eq 'tYES'")) ok &&= str(order, 'Cancelled') === 'tYES';
  if (filt.includes('DocNum eq ')) {
    const mpart = filt.split('DocNum eq ')[1].split(' ')[0].replace(')', '');
    ok &&= String(intN(order, 'DocNum') ?? '') === mpart;
  }
  if (filt.includes("CardCode eq '")) {
    const code = filt.split("CardCode eq '")[1].split("'")[0];
    ok &&= str(order, 'CardCode') === code;
  }
  if (filt.includes('SalesPersonCode eq ')) {
    const code = filt.split('SalesPersonCode eq ')[1].split(' ')[0].replace(')', '');
    ok &&= String(intN(order, 'SalesPersonCode') ?? '') === code;
  }
  return ok;
}

let _client: MockB1Client | null = null;
// Client mock dùng chung (mock mode)
export function getB1Client(): MockB1Client {
  if (!_client) _client = new MockB1Client();
  return _client;
}

// ---------------- DTO mapping (snake_case, khớp API .NET) ----------------
export interface OrderLineOut {
  line_num: number;
  item_code: string;
  description: string;
  quantity: number;
  price: number;
  discount_percent: number;
  warehouse: string;
  tax_code: string;
  ship_date: string;
  project_code: string;
  costing_code: string;
  uom_code: string;
  line_status: string;
  line_total: number | null;
}

export interface OrderOut {
  doc_entry: number;
  doc_num: number;
  doc_date: string;
  doc_due_date: string;
  tax_date: string;
  card_code: string;
  card_name: string;
  num_at_card: string;
  doc_total: number;
  currency: string;
  doc_rate: number;
  status: 'open' | 'closed' | 'cancelled';
  comments: string;
  sales_person_code: number | null;
  contact_person_code: number | null;
  ship_to_code: string;
  pay_to_code: string;
  payment_group_code: number | null;
  transportation_code: number | null;
  project: string;
  series: number | null;
  discount_percent: number;
  address: string;
  external_id: string | null;
  lines: OrderLineOut[];
}

export function normalizeOrder(raw: Rec): OrderOut {
  const cancelled = str(raw, 'Cancelled') === 'tYES';
  let docStatus = str(raw, 'DocumentStatus') || 'bost_Open';
  const status = cancelled ? 'cancelled' : docStatus === 'bost_Close' ? 'closed' : 'open';
  const lines: OrderLineOut[] = dictList(raw, 'DocumentLines').map((ln) => ({
    line_num: intN(ln, 'LineNum') ?? 0,
    item_code: str(ln, 'ItemCode'),
    description: str(ln, 'ItemDescription'),
    quantity: num(ln, 'Quantity'),
    price: ln['Price'] !== null && ln['Price'] !== undefined ? num(ln, 'Price') : num(ln, 'UnitPrice'),
    discount_percent: num(ln, 'DiscountPercent'),
    warehouse: str(ln, 'WarehouseCode'),
    tax_code: str(ln, 'TaxCode'),
    ship_date: str(ln, 'ShipDate'),
    project_code: str(ln, 'ProjectCode'),
    costing_code: str(ln, 'CostingCode'),
    uom_code: str(ln, 'UoMCode'),
    line_status: str(ln, 'LineStatus'),
    line_total:
      ln['LineTotal'] !== null && ln['LineTotal'] !== undefined ? num(ln, 'LineTotal') : null,
  }));
  return {
    doc_entry: intN(raw, 'DocEntry') ?? 0,
    doc_num: intN(raw, 'DocNum') ?? 0,
    doc_date: str(raw, 'DocDate'),
    doc_due_date: str(raw, 'DocDueDate'),
    tax_date: str(raw, 'TaxDate'),
    card_code: str(raw, 'CardCode'),
    card_name: str(raw, 'CardName'),
    num_at_card: str(raw, 'NumAtCard'),
    doc_total: num(raw, 'DocTotal'),
    currency: str(raw, 'DocCurrency'),
    doc_rate: num(raw, 'DocRate', 1),
    status: status as OrderOut['status'],
    comments: str(raw, 'Comments'),
    sales_person_code: intN(raw, 'SalesPersonCode'),
    contact_person_code: intN(raw, 'ContactPersonCode'),
    ship_to_code: str(raw, 'ShipToCode'),
    pay_to_code: str(raw, 'PayToCode'),
    payment_group_code: intN(raw, 'PaymentGroupCode'),
    transportation_code: intN(raw, 'TransportationCode'),
    project: str(raw, 'Project'),
    series: intN(raw, 'Series'),
    discount_percent: num(raw, 'DiscountPercent'),
    address: str(raw, 'Address'),
    external_id: raw[UDF] !== null && raw[UDF] !== undefined ? String(raw[UDF]) : null,
    lines,
  };
}

// ---------------- Order service ----------------
// Dịch lỗi B1 sang HTTP status + tiếng Việt
export function b1ErrorToHttp(ex: unknown): { status: number; message: string } {
  const msg = ex instanceof Error ? ex.message : String(ex);
  if (msg.includes('412')) return { status: 409, message: 'Đơn hàng đã bị người khác sửa. Vui lòng tải lại rồi thử tiếp.' };
  if (msg.includes('404')) return { status: 404, message: 'Không tìm thấy đơn hàng.' };
  if (msg.includes('401') || msg.includes('Login'))
    return { status: 502, message: 'Không kết nối được SAP B1 (sai thông tin đăng nhập?).' };
  const detail = msg.replace('B1 lỗi 400: ', '').replace('B1 lỗi ', '');
  return { status: 400, message: `Lỗi từ SAP B1: ${detail}` };
}

export interface OrderCreateIn {
  external_id?: string;
  card_code: string;
  doc_date?: string;
  doc_due_date?: string;
  tax_date?: string;
  num_at_card?: string;
  doc_currency?: string;
  doc_rate?: number;
  comments?: string;
  /** Alias tương thích frontend React (gửi 'notes') */
  notes?: string;
  discount_percent?: number;
  /** Alias tương thích frontend React (gửi 'doc_discount') */
  doc_discount?: number;
  sales_person_code?: number;
  lines: {
    item_code: string;
    quantity: number;
    price: number;
    warehouse?: string;
    tax_code?: string;
    discount_percent?: number;
    /** Alias tương thích frontend React (gửi 'discount') */
    discount?: number;
    ship_date?: string;
  }[];
}

export function toB1CreatePayload(order: OrderCreateIn): Rec {
  const lines: Rec[] = order.lines.map((ln) => {
    const b1Line: Rec = {
      ItemCode: ln.item_code,
      Quantity: ln.quantity,
      Price: ln.price,
      DiscountPercent: ln.discount_percent ?? 0,
    };
    if (ln.warehouse) b1Line['WarehouseCode'] = ln.warehouse;
    if (ln.tax_code) b1Line['TaxCode'] = ln.tax_code;
    if (ln.ship_date) b1Line['ShipDate'] = ln.ship_date;
    return b1Line;
  });
  const docDate = order.doc_date || todayStr();
  const payload: Rec = {
    CardCode: order.card_code,
    DocDate: docDate,
    TaxDate: order.tax_date || docDate,
    DocumentLines: lines,
    DiscountPercent: order.discount_percent ?? 0,
  };
  if (order.doc_due_date) payload['DocDueDate'] = order.doc_due_date;
  if (order.num_at_card) payload['NumAtCard'] = order.num_at_card;
  if (order.doc_currency) payload['DocCurrency'] = order.doc_currency;
  if (order.doc_rate !== undefined) payload['DocRate'] = order.doc_rate;
  if (order.comments) payload['Comments'] = order.comments;
  if (order.sales_person_code !== undefined) payload['SalesPersonCode'] = order.sales_person_code;
  if (order.external_id) payload[UDF] = order.external_id;
  return payload;
}

// Ghi audit SL cho mỗi thao tác (giống AuditedAsync)
function audited<T>(
  operation: string,
  entityKey: string,
  username: string,
  requestData: unknown,
  fn: () => T
): T {
  const start = Date.now();
  try {
    const retval = fn();
    const key =
      entityKey ||
      (retval && typeof retval === 'object' && 'doc_entry' in (retval as object)
        ? String((retval as unknown as { doc_entry: number }).doc_entry)
        : '');
    logSlAudit({
      operation,
      entity_key: key,
      username,
      request_json: JSON.stringify(requestData ?? {}),
      success: true,
      response_json: JSON.stringify(retval ?? {}),
      duration_ms: Date.now() - start,
    });
    return retval;
  } catch (ex) {
    const msg = ex instanceof Error ? ex.message : String(ex);
    logSlAudit({
      operation,
      entity_key: entityKey,
      username,
      request_json: JSON.stringify(requestData ?? {}),
      success: false,
      error: msg,
      duration_ms: Date.now() - start,
    });
    throw ex;
  }
}

export function createOrder(order: OrderCreateIn, username: string): { order: OrderOut; created: boolean } {
  return audited('order.create', order.external_id || '', username, order, () => {
    const b1 = getB1Client();
    if (order.external_id) {
      const existed = b1.findOrderByExternalId(order.external_id);
      if (existed) {
        const { order: full } = b1.getOrder(intN(existed, 'DocEntry') ?? 0);
        return { order: normalizeOrder(full), created: false };
      }
    }
    const raw = b1.createOrder(toB1CreatePayload(order));
    return { order: normalizeOrder(raw), created: true };
  });
}

export function getOrder(docEntry: number): { order: OrderOut; etag: string } {
  const b1 = getB1Client();
  try {
    const { order, etag } = b1.getOrder(docEntry);
    return { order: normalizeOrder(order), etag };
  } catch (ex) {
    const { status, message } = b1ErrorToHttp(ex);
    throw Object.assign(new Error(message), { status });
  }
}

export function buildFilter(
  status: string,
  cardCode?: string | null,
  docNum?: number | null,
  salesPersonCode?: number | null
): string | undefined {
  const parts: string[] = [];
  if (status === 'open') parts.push("DocumentStatus eq 'bost_Open' and Cancelled eq 'tNO'");
  else if (status === 'closed') parts.push("DocumentStatus eq 'bost_Close' and Cancelled eq 'tNO'");
  else if (status === 'cancelled') parts.push("Cancelled eq 'tYES'");
  if (cardCode) parts.push(`CardCode eq '${cardCode.replace(/'/g, "''")}'`);
  if (docNum !== null && docNum !== undefined) parts.push(`DocNum eq ${docNum}`);
  if (salesPersonCode !== null && salesPersonCode !== undefined)
    parts.push(`SalesPersonCode eq ${salesPersonCode}`);
  return parts.length ? parts.join(' and ') : undefined;
}

export function listOrders(
  status: string,
  cardCode: string | null,
  docNum: number | null,
  salesPersonCode: number | null,
  top: number,
  skip: number
): { total: number; items: OrderOut[] } {
  const b1 = getB1Client();
  try {
    const data = b1.listOrders(top, skip, buildFilter(status, cardCode, docNum, salesPersonCode));
    return { total: data.total, items: data.value.map(normalizeOrder) };
  } catch (ex) {
    const { status: s, message } = b1ErrorToHttp(ex);
    throw Object.assign(new Error(message), { status: s });
  }
}

export function cancelOrder(docEntry: number, username: string): OrderOut {
  return audited('order.cancel', String(docEntry), username, {}, () => {
    const raw = getB1Client().cancelOrder(docEntry);
    return normalizeOrder(raw);
  });
}

export function closeOrder(docEntry: number, lineNums: number[] | null, username: string): OrderOut {
  return audited('order.close', String(docEntry), username, { line_nums: lineNums }, () => {
    const b1 = getB1Client();
    b1.closeOrderLines(docEntry, lineNums);
    const { order } = b1.getOrder(docEntry);
    return normalizeOrder(order);
  });
}

export interface DashboardStats {
  total_orders: number;
  total_revenue: number;
  open_orders: number;
  closed_orders: number;
  cancelled_orders: number;
  revenue_by_day: { date: string; revenue: number; orders: number }[];
  top_customers: { card_code: string; card_name: string; revenue: number; orders: number }[];
  top_items: { item_code: string; item_name: string; quantity: number; revenue: number }[];
  scope_note: string;
  // alias cho frontend cũ
  total_amount: number;
}

export function getDashboardStats(salesPersonCode?: number | null): DashboardStats {
  const { total, items } = listOrders('', null, null, salesPersonCode ?? null, 1000, 0);
  const orders = items;
  const scopeSuffix = salesPersonCode ? ' của bạn' : '';
  const nonCancelled = orders.filter((o) => o.status !== 'cancelled');
  const stats: DashboardStats = {
    total_orders: total,
    total_revenue: nonCancelled.reduce((s, o) => s + o.doc_total, 0),
    open_orders: orders.filter((o) => o.status === 'open').length,
    closed_orders: orders.filter((o) => o.status === 'closed').length,
    cancelled_orders: orders.filter((o) => o.status === 'cancelled').length,
    revenue_by_day: [],
    top_customers: [],
    top_items: [],
    scope_note:
      total > 1000
        ? `Số liệu trong 1.000 đơn gần nhất${scopeSuffix}`
        : `Số liệu toàn bộ đơn hàng${scopeSuffix}`,
    total_amount: 0,
  };
  stats.total_amount = stats.total_revenue;
  // Doanh thu 30 ngày
  const byDay = new Map<string, { revenue: number; orders: number }>();
  for (const o of nonCancelled) {
    const d = o.doc_date?.slice(0, 10);
    if (!d) continue;
    const cur = byDay.get(d) || { revenue: 0, orders: 0 };
    cur.revenue += o.doc_total;
    cur.orders += 1;
    byDay.set(d, cur);
  }
  const now = new Date();
  for (let i = 29; i >= 0; i--) {
    const d = new Date(now);
    d.setDate(d.getDate() - i);
    const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
    const v = byDay.get(key) || { revenue: 0, orders: 0 };
    stats.revenue_by_day.push({
      date: `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`,
      revenue: v.revenue,
      orders: v.orders,
    });
  }
  // Top 5 khách hàng
  const custMap = new Map<string, { card_code: string; card_name: string; revenue: number; orders: number }>();
  for (const o of nonCancelled) {
    const cur = custMap.get(o.card_code) || {
      card_code: o.card_code,
      card_name: o.card_name,
      revenue: 0,
      orders: 0,
    };
    cur.revenue += o.doc_total;
    cur.orders += 1;
    custMap.set(o.card_code, cur);
  }
  stats.top_customers = [...custMap.values()].sort((a, b) => b.revenue - a.revenue).slice(0, 5);
  // Top 5 hàng hoá (mock luôn có lines)
  const itemMap = new Map<string, { item_code: string; item_name: string; quantity: number; revenue: number }>();
  for (const o of nonCancelled)
    for (const l of o.lines) {
      const cur = itemMap.get(l.item_code) || {
        item_code: l.item_code,
        item_name: l.description,
        quantity: 0,
        revenue: 0,
      };
      cur.quantity += l.quantity;
      cur.revenue += l.line_total ?? l.quantity * l.price;
      itemMap.set(l.item_code, cur);
    }
  stats.top_items = [...itemMap.values()].sort((a, b) => b.revenue - a.revenue).slice(0, 5);
  return stats;
}

// Master data helpers (ưu tiên mock client trực tiếp ở chế độ mock)
export function searchCustomers(q: string) {
  return getB1Client()
    .searchCustomers(q || '')
    .map((r) => ({
      card_code: str(r, 'CardCode'),
      card_name: str(r, 'CardName'),
      phone: str(r, 'Phone1'),
      sales_person_code: intN(r, 'SalesPersonCode'),
      price_list_num: intN(r, 'PriceListNum'),
    }));
}

export function searchItems(q: string) {
  return getB1Client()
    .searchItems(q || '')
    .map((r) => ({
      item_code: str(r, 'ItemCode'),
      item_name: str(r, 'ItemName'),
      sales_unit: str(r, 'SalesUnit'),
      price: num(r, 'Price'),
      tax_code: str(r, 'TaxCode'),
    }));
}

// Đồng bộ master data mock vào DB local (cho tra cứu nhanh)
export function syncMockMasterData(companyDb: string): Record<string, number> {
  const db = require('./db').getDb() as import('better-sqlite3').Database;
  const b1 = getB1Client();
  const counts: Record<string, number> = {};
  const customers = b1.fetchCustomersPage(500, 0);
  const insC = db.prepare(
    `INSERT INTO md_customers (company_db, card_code, card_name, phone, sales_person_code, price_list_num, synced_at)
     VALUES (?, ?, ?, ?, ?, ?, datetime('now'))
     ON CONFLICT(company_db, card_code) DO UPDATE SET card_name=excluded.card_name, phone=excluded.phone,
       sales_person_code=excluded.sales_person_code, price_list_num=excluded.price_list_num, synced_at=datetime('now')`
  );
  const txC = db.transaction((rows: Rec[]) => {
    for (const r of rows)
      insC.run(companyDb, str(r, 'CardCode'), str(r, 'CardName'), str(r, 'Phone1'), intN(r, 'SalesPersonCode'), intN(r, 'PriceListNum'));
  });
  txC(customers);
  counts['customers'] = customers.length;

  const items = b1.fetchItemsPage(500, 0);
  const insI = db.prepare(
    `INSERT INTO md_items (company_db, item_code, item_name, sales_unit, price, tax_code, synced_at)
     VALUES (?, ?, ?, ?, ?, ?, datetime('now'))
     ON CONFLICT(company_db, item_code) DO UPDATE SET item_name=excluded.item_name, sales_unit=excluded.sales_unit,
       price=excluded.price, tax_code=excluded.tax_code, synced_at=datetime('now')`
  );
  const txI = db.transaction((rows: Rec[]) => {
    for (const r of rows)
      insI.run(companyDb, str(r, 'ItemCode'), str(r, 'ItemName'), str(r, 'SalesUnit'), num(r, 'Price'), str(r, 'TaxCode'));
  });
  txI(items);
  counts['items'] = items.length;

  const taxes = b1.fetchTaxCodes();
  const insT = db.prepare(
    `INSERT INTO md_tax_codes (company_db, code, name, rate, synced_at)
     VALUES (?, ?, ?, ?, datetime('now'))
     ON CONFLICT(company_db, code) DO UPDATE SET name=excluded.name, rate=excluded.rate, synced_at=datetime('now')`
  );
  const txT = db.transaction((rows: Rec[]) => {
    for (const r of rows) insT.run(companyDb, str(r, 'Code'), str(r, 'Name'), num(r, 'Rate'));
  });
  txT(taxes);
  counts['tax_codes'] = taxes.length;

  // Ghi sync log
  const now = new Date().toISOString().slice(0, 19).replace('T', ' ');
  for (const [entity, n] of Object.entries(counts)) {
    db.prepare(
      `INSERT INTO md_sync_log (entity, company_db, status, started_at, finished_at, records, error)
       VALUES (?, ?, 'done', ?, ?, ?, '')`
    ).run(entity, companyDb, now, now, n);
  }
  return counts;
}
