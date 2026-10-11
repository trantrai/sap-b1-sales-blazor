// lib/db.ts — SQLite local: tài khoản web, phân quyền, audit log.
// B1 là source of truth cho dữ liệu đơn hàng — DB này KHÔNG lưu đơn hàng.
import Database from 'better-sqlite3';
import path from 'path';
import fs from 'fs';
import { hashPassword } from './auth';

const DATA_DIR = process.env.DATA_DIR || path.join(process.cwd(), 'data');
const DB_PATH = path.join(DATA_DIR, 'app.db');

let _db: Database.Database | null = null;

// Lấy kết nối singleton (khởi tạo schema + seed admin lần đầu)
export function getDb(): Database.Database {
  if (_db) return _db;
  fs.mkdirSync(DATA_DIR, { recursive: true });
  _db = new Database(DB_PATH);
  _db.pragma('journal_mode = WAL');
  initSchema(_db);
  return _db;
}

function initSchema(db: Database.Database) {
  db.exec(`
    CREATE TABLE IF NOT EXISTS users (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      username TEXT UNIQUE NOT NULL,
      password_hash TEXT NOT NULL,
      full_name TEXT DEFAULT '',
      role TEXT NOT NULL DEFAULT 'viewer',
      sales_person_code INTEGER,
      active INTEGER NOT NULL DEFAULT 1,
      created_at TEXT DEFAULT (datetime('now'))
    );
    CREATE TABLE IF NOT EXISTS audit_logs (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      username TEXT NOT NULL,
      action TEXT NOT NULL,
      entity TEXT NOT NULL,
      entity_key TEXT,
      detail TEXT,
      ip TEXT,
      created_at TEXT DEFAULT (datetime('now'))
    );
    CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_logs(created_at);
    CREATE TABLE IF NOT EXISTS app_settings (
      key TEXT PRIMARY KEY,
      value TEXT NOT NULL DEFAULT '',
      updated_at TEXT DEFAULT (datetime('now'))
    );
    CREATE TABLE IF NOT EXISTS md_sync_log (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      entity TEXT NOT NULL,
      company_db TEXT NOT NULL DEFAULT '',
      status TEXT NOT NULL,
      started_at TEXT DEFAULT (datetime('now')),
      finished_at TEXT,
      records INTEGER NOT NULL DEFAULT 0,
      error TEXT DEFAULT ''
    );
    CREATE INDEX IF NOT EXISTS idx_md_sync_log_entity ON md_sync_log(company_db, entity, id);
    CREATE TABLE IF NOT EXISTS md_customers (
      company_db TEXT NOT NULL DEFAULT '',
      card_code TEXT NOT NULL,
      card_name TEXT NOT NULL DEFAULT '',
      phone TEXT DEFAULT '',
      sales_person_code INTEGER,
      price_list_num INTEGER,
      updated_at TEXT DEFAULT '',
      synced_at TEXT DEFAULT (datetime('now')),
      PRIMARY KEY (company_db, card_code)
    );
    CREATE INDEX IF NOT EXISTS idx_md_customers_name ON md_customers(company_db, card_name);
    CREATE TABLE IF NOT EXISTS md_items (
      company_db TEXT NOT NULL DEFAULT '',
      item_code TEXT NOT NULL,
      item_name TEXT NOT NULL DEFAULT '',
      sales_unit TEXT DEFAULT '',
      price REAL NOT NULL DEFAULT 0,
      tax_code TEXT DEFAULT '',
      updated_at TEXT DEFAULT '',
      synced_at TEXT DEFAULT (datetime('now')),
      PRIMARY KEY (company_db, item_code)
    );
    CREATE INDEX IF NOT EXISTS idx_md_items_name ON md_items(company_db, item_name);
    CREATE TABLE IF NOT EXISTS md_tax_codes (
      company_db TEXT NOT NULL DEFAULT '',
      code TEXT NOT NULL,
      name TEXT NOT NULL DEFAULT '',
      rate REAL NOT NULL DEFAULT 0,
      synced_at TEXT DEFAULT (datetime('now')),
      PRIMARY KEY (company_db, code)
    );
    CREATE TABLE IF NOT EXISTS sl_audit (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      created_at TEXT DEFAULT (datetime('now')),
      operation TEXT NOT NULL,
      entity_key TEXT DEFAULT '',
      username TEXT DEFAULT '',
      request_json TEXT DEFAULT '{}',
      success INTEGER NOT NULL DEFAULT 0,
      http_status INTEGER,
      response_json TEXT DEFAULT '',
      error TEXT DEFAULT '',
      duration_ms INTEGER NOT NULL DEFAULT 0,
      retry_count INTEGER NOT NULL DEFAULT 0,
      retried_from INTEGER,
      note TEXT DEFAULT ''
    );
    CREATE INDEX IF NOT EXISTS idx_sl_audit_created ON sl_audit(created_at DESC);
    CREATE INDEX IF NOT EXISTS idx_sl_audit_op ON sl_audit(operation);
    CREATE INDEX IF NOT EXISTS idx_sl_audit_success ON sl_audit(success);
    CREATE TABLE IF NOT EXISTS refresh_tokens (
      id INTEGER PRIMARY KEY AUTOINCREMENT,
      token_hash TEXT NOT NULL UNIQUE,
      username TEXT NOT NULL,
      company_db TEXT DEFAULT '',
      expires_at TEXT NOT NULL,
      created_at TEXT DEFAULT (datetime('now')),
      revoked INTEGER NOT NULL DEFAULT 0
    );
    CREATE INDEX IF NOT EXISTS idx_refresh_hash ON refresh_tokens(token_hash);
    CREATE INDEX IF NOT EXISTS idx_refresh_user ON refresh_tokens(username);
  `);

  // Seed tài khoản admin lần đầu
  const adminUser = process.env.ADMIN_USERNAME || 'admin';
  const adminPass = process.env.ADMIN_PASSWORD || 'admin123';
  const exists = db.prepare('SELECT id FROM users WHERE username = ?').get(adminUser);
  if (!exists) {
    db.prepare(
      "INSERT INTO users (username, password_hash, full_name, role) VALUES (?, ?, 'Administrator', 'admin')"
    ).run(adminUser, hashPassword(adminPass));
  }
}

// Ghi audit log thao tác trên web (bảng audit_logs)
export function logAction(
  username: string,
  action: string,
  entity = 'Orders',
  entityKey: string | null = null,
  detail: Record<string, unknown> | null = null,
  ip: string | null = null
) {
  const db = getDb();
  db.prepare(
    'INSERT INTO audit_logs (username, action, entity, entity_key, detail, ip) VALUES (?, ?, ?, ?, ?, ?)'
  ).run(username, action, entity, entityKey, detail ? JSON.stringify(detail) : '{}', ip);
}

// Ghi audit Service Layer (bảng sl_audit)
export function logSlAudit(entry: {
  operation: string;
  entity_key: string;
  username: string;
  request_json: string;
  success: boolean;
  http_status?: number | null;
  response_json?: string;
  error?: string;
  duration_ms: number;
  retry_count?: number;
  retried_from?: number | null;
  note?: string;
}) {
  const db = getDb();
  db.prepare(
    `INSERT INTO sl_audit (operation, entity_key, username, request_json, success, http_status,
      response_json, error, duration_ms, retry_count, retried_from, note)
     VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
  ).run(
    entry.operation,
    entry.entity_key,
    entry.username,
    entry.request_json,
    entry.success ? 1 : 0,
    entry.http_status ?? null,
    entry.response_json ?? '',
    entry.error ?? '',
    entry.duration_ms,
    entry.retry_count ?? 0,
    entry.retried_from ?? null,
    entry.note ?? ''
  );
}

// Đọc/ghi app_settings
export function getSetting(key: string): string | null {
  const db = getDb();
  const row = db.prepare('SELECT value FROM app_settings WHERE key = ?').get(key) as
    | { value: string }
    | undefined;
  return row ? row.value : null;
}

export function setSetting(key: string, value: string) {
  const db = getDb();
  db.prepare(
    `INSERT INTO app_settings (key, value, updated_at) VALUES (?, ?, datetime('now'))
     ON CONFLICT(key) DO UPDATE SET value = excluded.value, updated_at = datetime('now')`
  ).run(key, value);
}
