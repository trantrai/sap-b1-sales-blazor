"""Controller cho child DocType B1 Sales Order Item."""

import frappe
from frappe.model.document import Document


class B1SalesOrderItem(Document):
    def validate(self):
        qty = self.quantity or 0
        price = self.price or 0
        disc = self.discount_percent or 0
        self.line_total = qty * price * (1 - disc / 100)
