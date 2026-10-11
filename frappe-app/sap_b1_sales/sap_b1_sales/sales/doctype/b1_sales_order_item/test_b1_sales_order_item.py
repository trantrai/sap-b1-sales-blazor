"""Unit test cho B1 Sales Order Item."""

import frappe
from frappe.tests.utils import FrappeTestCase


class TestB1SalesOrderItem(FrappeTestCase):
    def test_line_total(self):
        doc = frappe.get_doc({
            "doctype": "B1 Sales Order Item",
            "item_code": "A00001",
            "quantity": 2,
            "price": 1500000,
            "discount_percent": 10,
        })
        doc.validate()
        self.assertEqual(doc.line_total, 2700000)
