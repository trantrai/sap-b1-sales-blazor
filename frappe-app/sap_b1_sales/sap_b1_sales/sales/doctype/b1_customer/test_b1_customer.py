"""Unit test cho B1 Customer."""

import frappe
from frappe.tests.utils import FrappeTestCase


class TestB1Customer(FrappeTestCase):
    def test_card_code_unique(self):
        doc = frappe.get_doc({
            "doctype": "B1 Customer",
            "card_code": "C20000",
            "card_name": "Công ty TNHH An Phát",
        })
        self.assertEqual(doc.card_code, "C20000")
