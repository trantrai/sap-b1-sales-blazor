"""Unit test cho B1 Sales Order."""

import frappe
from frappe.tests.utils import FrappeTestCase


class TestB1SalesOrder(FrappeTestCase):
    def test_doc_total_with_discount(self):
        doc = frappe.get_doc({
            "doctype": "B1 Sales Order",
            "card_code": "C20000",
            "doc_date": "2026-10-11",
            "discount_percent": 10,
            "items": [
                {"item_code": "A00001", "quantity": 2, "price": 1500000},
            ],
        })
        doc.validate()
        self.assertEqual(doc.doc_total, 2700000)

    def test_requires_items(self):
        doc = frappe.get_doc({
            "doctype": "B1 Sales Order",
            "card_code": "C20000",
            "doc_date": "2026-10-11",
            "items": [],
        })
        self.assertRaises(frappe.ValidationError, doc.validate)
