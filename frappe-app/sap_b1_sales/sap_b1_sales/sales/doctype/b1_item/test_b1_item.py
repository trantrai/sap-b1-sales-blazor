"""Unit test cho B1 Item."""

import frappe
from frappe.tests.utils import FrappeTestCase


class TestB1Item(FrappeTestCase):
    def test_item_code_unique(self):
        doc = frappe.get_doc({
            "doctype": "B1 Item",
            "item_code": "A00001",
            "item_name": "Bàn làm việc gỗ",
        })
        self.assertEqual(doc.item_code, "A00001")
