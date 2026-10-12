"""API xác thực — tương đương AuthController.cs (.NET).

Endpoint Frappe: /api/method/sap_b1_sales.b1_integration.api.auth.<ten_ham>

Xác thực: Frappe dùng session cookie hoặc token (Authorization: token api_key:api_secret).
Hàm login() bên dưới xác thực user rồi cấp API key/secret để client dùng tiếp.
"""

import frappe


def _databases():
    from sap_b1_sales.b1_integration.doctype.b1_connection_settings.b1_connection_settings import (
        get_connection_settings,
    )
    settings = get_connection_settings()
    dbs = settings.get("databases") or []
    default = settings.get("b1_company_db") or (dbs[0]["code"] if dbs else "")
    return dbs, default


@frappe.whitelist(allow_guest=True)
def get_databases():
    """GET /api/auth/databases — danh sách CompanyDB (public)."""
    dbs, default = _databases()
    return {"databases": dbs, "default": default}


@frappe.whitelist(allow_guest=True)
def login(username: str, password: str, company_db: str = ""):
    """POST /api/auth/login — đăng nhập, trả về token (API key/secret)."""
    from frappe.auth import LoginManager

    dbs, default = _databases()
    db = (company_db or "").strip() or default
    if dbs and not any(d["code"].lower() == db.lower() for d in dbs):
        frappe.throw(f"Database '{db}' chưa được khai báo trong hệ thống.")

    login_manager = LoginManager()
    login_manager.authenticate(username, password)
    login_manager.post_login()

    # Cấp API key/secret cho client (header: Authorization: token key:secret)
    # theo đúng pattern frappe.core.doctype.user.user.generate_keys
    user_doc = frappe.get_doc("User", username)
    api_secret_plain = frappe.generate_hash(length=15)
    if not user_doc.api_key:
        user_doc.api_key = frappe.generate_hash(length=15)
    user_doc.api_secret = api_secret_plain
    user_doc.save(ignore_permissions=True)
    api_key = user_doc.api_key
    api_secret = user_doc.get_password("api_secret")

    frappe.db.commit()
    _audit(username, "auth.login", "User", username, {"company_db": db})
    return {
        "access_token": f"{api_key}:{api_secret}",
        "token_type": "token",
        "refresh_token": "",
        "expires_in": 0,
        "company_db": db,
    }


@frappe.whitelist()
def logout():
    """POST /api/auth/logout."""
    user = frappe.session.user
    frappe.local.login_manager.logout()
    _audit(user, "auth.logout", "User", user, {})
    return {"ok": True}


@frappe.whitelist()
def me():
    """GET /api/auth/me — thông tin user hiện tại."""
    user = frappe.get_doc("User", frappe.session.user)
    roles = frappe.get_roles(frappe.session.user)
    role = "admin" if "System Manager" in roles else (
        "manager" if "Sales Manager" in roles else (
            "sales" if "Sales User" in roles else "viewer"))
    return {
        "username": user.name,
        "full_name": user.full_name,
        "role": role,
        "active": not user.enabled == 0,
    }


@frappe.whitelist()
def list_users():
    """GET /api/auth/users — danh sách user (cần quyền)."""
    frappe.only_for(("System Manager", "Sales Manager"))
    users = frappe.get_all(
        "User",
        filters={"user_type": "System User", "name": ["not in", ["Administrator", "Guest"]]},
        fields=["name", "full_name", "enabled"],
    )
    out = []
    for u in users:
        roles = frappe.get_roles(u.name)
        role = "admin" if "System Manager" in roles else (
            "manager" if "Sales Manager" in roles else (
                "sales" if "Sales User" in roles else "viewer"))
        out.append({
            "username": u.name,
            "full_name": u.full_name,
            "role": role,
            "active": bool(u.enabled),
        })
    return out


def _audit(username, action, entity_type, entity_id, data):
    try:
        frappe.get_doc({
            "doctype": "B1 API Audit Log",
            "username": username,
            "action": action,
            "entity_type": entity_type,
            "entity_id": entity_id,
            "result": "Thành công",
        }).insert(ignore_permissions=True)
    except Exception:
        pass
