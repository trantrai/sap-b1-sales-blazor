"""Client SAP Business One Service Layer — port từ SalesWeb.Server/Services/B1 (C#).

- B1ServiceLayerClient: kết nối thật, quản lý session qua cookie B1SESSION,
  tự login lại khi gặp 401, hỗ trợ ETag/If-Match.
- MockB1Client: dữ liệu trong RAM, dùng khi b1_mock=1 (mặc định cho demo).
"""

from __future__ import annotations

import threading
import time
from typing import Any

import requests


class B1Exception(Exception):
    """Lỗi trả về từ Service Layer."""


class B1ServiceLayerClient:
    """Client Service Layer thật."""

    def __init__(
        self,
        base_url: str,
        company_db: str,
        username: str,
        password: str,
        verify_ssl: bool = True,
        timeout: float = 30.0,
    ):
        self.base_url = base_url.rstrip("/")
        self.company_db = company_db
        self.username = username
        self.password = password
        self.timeout = timeout
        self._session = requests.Session()
        self._session.verify = verify_ssl
        self._session.headers.update({"Content-Type": "application/json"})
        self._logged_in = False
        self._lock = threading.Lock()

    # ---------- session ----------

    def login(self) -> None:
        with self._lock:
            if self._logged_in:
                return
            resp = self._session.post(
                f"{self.base_url}/Login",
                json={
                    "CompanyDB": self.company_db,
                    "UserName": self.username,
                    "Password": self.password,
                },
                timeout=self.timeout,
            )
            if resp.status_code != 200:
                raise B1Exception(
                    f"Login B1 thất bại ({resp.status_code}): {resp.text[:500]}"
                )
            self._logged_in = True

    def logout(self) -> None:
        try:
            self._session.post(f"{self.base_url}/Logout", timeout=self.timeout)
        except Exception:
            pass
        self._logged_in = False

    def _request(self, method: str, path: str, **kwargs) -> requests.Response:
        if not self._logged_in:
            self.login()
        url = f"{self.base_url}/{path.lstrip('/')}"
        kwargs.setdefault("timeout", self.timeout)
        resp = self._session.request(method, url, **kwargs)
        if resp.status_code == 401:
            # session hết hạn -> login lại, retry 1 lần
            self._logged_in = False
            self.login()
            resp = self._session.request(method, url, **kwargs)
        return resp

    def _raise(self, resp: requests.Response) -> dict[str, Any]:
        if resp.status_code == 412:
            raise B1Exception(
                "B1 lỗi 412: Đơn hàng đã bị người khác sửa (xung đột ETag). Hãy tải lại."
            )
        if resp.status_code >= 400:
            try:
                err = resp.json().get("error", {}).get("message", {}).get("value")
            except Exception:
                err = None
            raise B1Exception(
                f"B1 lỗi {(resp.status_code)}: {err or resp.text[:300]}"
            )
        try:
            return resp.json() if resp.text else {}
        except Exception:
            return {}

    # ---------- Sales Orders ----------

    def create_order(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._raise(self._request("POST", "Orders", json=payload))

    def get_order(self, doc_entry: int) -> tuple[dict[str, Any], str | None]:
        resp = self._request("GET", f"Orders({doc_entry})")
        data = self._raise(resp)
        return data, resp.headers.get("ETag")

    def list_orders(
        self, top: int = 50, skip: int = 0,
        filter: str | None = None, orderby: str = "DocEntry desc",
    ) -> dict[str, Any]:
        params: dict[str, Any] = {"$top": top, "$skip": skip, "$orderby": orderby}
        if filter:
            params["$filter"] = filter
        return self._raise(self._request("GET", "Orders", params=params))

    def update_order(
        self, doc_entry: int, payload: dict[str, Any], etag: str | None = None
    ) -> None:
        headers = {"If-Match": etag} if etag else None
        self._raise(
            self._request("PATCH", f"Orders({doc_entry})", json=payload, headers=headers)
        )

    def cancel_order(self, doc_entry: int) -> dict[str, Any]:
        return self._raise(self._request("POST", f"Orders({doc_entry})/Cancel"))

    def close_order(self, doc_entry: int) -> dict[str, Any]:
        return self._raise(self._request("POST", f"Orders({doc_entry})/Close"))

    # ---------- Master data ----------

    def search_business_partners(self, q: str, top: int = 20) -> list[dict[str, Any]]:
        data = self._raise(
            self._request(
                "GET", "BusinessPartners",
                params={
                    "$top": top,
                    "$filter": f"startswith(CardCode,'{q}') or contains(CardName,'{q}')",
                    "$select": "CardCode,CardName,Phone,SalesPersonCode,PriceListNum",
                },
            )
        )
        return data.get("value", [])

    def search_items(self, q: str, top: int = 20) -> list[dict[str, Any]]:
        data = self._raise(
            self._request(
                "GET", "Items",
                params={
                    "$top": top,
                    "$filter": f"startswith(ItemCode,'{q}') or contains(ItemName,'{q}')",
                    "$select": "ItemCode,ItemName,SalesUnit,TaxCode",
                },
            )
        )
        return data.get("value", [])

    def list_warehouses(self) -> list[dict[str, Any]]:
        data = self._raise(
            self._request(
                "GET", "Warehouses",
                params={"$select": "WarehouseCode,WarehouseName"},
            )
        )
        return data.get("value", [])


class MockB1Client:
    """Mock trong RAM — tương đương MockB1Client.cs (đủ cho demo/test)."""

    def __init__(self):
        self._lock = threading.Lock()
        self._orders: dict[int, dict[str, Any]] = {}
        self._next_entry = 1002
        self._seed()

    def _seed(self) -> None:
        self._orders[1001] = {
            "DocEntry": 1001,
            "DocNum": 5001,
            "DocDate": "2026-10-11",
            "DocDueDate": "2026-10-11",
            "TaxDate": "2026-10-11",
            "CardCode": "C20000",
            "CardName": "Công ty TNHH An Phát",
            "DocTotal": 3000000.0,
            "DocCurrency": "VND",
            "DocumentStatus": "bost_Open",
            "Comments": "Đơn mẫu (mock)",
            "DocumentLines": [
                {
                    "LineNum": 0,
                    "ItemCode": "A00001",
                    "ItemDescription": "Bàn làm việc gỗ",
                    "Quantity": 2,
                    "Price": 1500000.0,
                    "DiscountPercent": 0,
                    "WarehouseCode": "01",
                    "TaxCode": "V10",
                    "LineStatus": "bost_Open",
                }
            ],
        }

    # API dùng chung tên với B1ServiceLayerClient để dễ thay thế
    def login(self) -> None: ...
    def logout(self) -> None: ...

    def create_order(self, payload: dict[str, Any]) -> dict[str, Any]:
        with self._lock:
            entry = self._next_entry
            self._next_entry += 1
            order = dict(payload)
            order["DocEntry"] = entry
            order["DocNum"] = 5000 + entry
            order.setdefault("DocumentStatus", "bost_Open")
            self._orders[entry] = order
            return order

    def get_order(self, doc_entry: int) -> tuple[dict[str, Any], str | None]:
        try:
            return self._orders[doc_entry], f'W/"{int(time.time())}"'
        except KeyError:
            raise B1Exception(f"Không tìm thấy đơn DocEntry={doc_entry}")

    def list_orders(self, top=50, skip=0, filter=None, orderby="DocEntry desc"):
        items = sorted(self._orders.values(), key=lambda o: -o["DocEntry"])
        if filter and "bost_Open" in filter:
            items = [o for o in items if o.get("DocumentStatus") == "bost_Open"]
        return {"value": items[skip: skip + top]}

    def update_order(self, doc_entry, payload, etag=None) -> None:
        order = self._orders.get(doc_entry)
        if not order:
            raise B1Exception(f"Không tìm thấy đơn DocEntry={doc_entry}")
        order.update(payload)

    def cancel_order(self, doc_entry: int) -> dict[str, Any]:
        order = self._orders.get(doc_entry)
        if not order:
            raise B1Exception(f"Không tìm thấy đơn DocEntry={doc_entry}")
        order["DocumentStatus"] = "bost_Cancelled"
        return order

    def close_order(self, doc_entry: int) -> dict[str, Any]:
        order = self._orders.get(doc_entry)
        if not order:
            raise B1Exception(f"Không tìm thấy đơn DocEntry={doc_entry}")
        order["DocumentStatus"] = "bost_Close"
        return order

    def search_business_partners(self, q: str, top: int = 20):
        bps = [
            {"CardCode": "C20000", "CardName": "Công ty TNHH An Phát",
             "Phone": "028-1234567", "SalesPersonCode": 1, "PriceListNum": 1},
            {"CardCode": "C20001", "CardName": "Công ty CP Bình Minh",
             "Phone": "028-7654321", "SalesPersonCode": 1, "PriceListNum": 1},
        ]
        q = q.lower()
        return [b for b in bps
                if q in b["CardCode"].lower() or q in b["CardName"].lower()][:top]

    def search_items(self, q: str, top: int = 20):
        items = [
            {"ItemCode": "A00001", "ItemName": "Bàn làm việc gỗ",
             "SalesUnit": "Cái", "TaxCode": "V10"},
            {"ItemCode": "A00002", "ItemName": "Ghế xoay văn phòng",
             "SalesUnit": "Cái", "TaxCode": "V10"},
        ]
        q = q.lower()
        return [i for i in items
                if q in i["ItemCode"].lower() or q in i["ItemName"].lower()][:top]

    def list_warehouses(self):
        return [
            {"WarehouseCode": "01", "WarehouseName": "Kho chính"},
            {"WarehouseCode": "02", "WarehouseName": "Kho phụ"},
        ]


def get_b1_client(settings: dict[str, Any]):
    """Factory: chọn Real/Mock theo settings['b1_mock']."""
    if settings.get("b1_mock"):
        return MockB1Client()
    return B1ServiceLayerClient(
        base_url=settings["b1_service_layer_url"],
        company_db=settings["b1_company_db"],
        username=settings["b1_username"],
        password=settings["b1_password"],
        verify_ssl=settings.get("b1_verify_ssl", True),
        timeout=float(settings.get("b1_timeout", 30)),
    )
