"""Unit test cho B1 Connection Settings."""

import frappe
from frappe.tests.utils import FrappeTestCase


class TestB1ConnectionSettings(FrappeTestCase):
    def test_single_exists(self):
        doc = frappe.get_single("B1 Connection Settings")
        self.assertTrue(doc.b1_mock in (0, 1))
