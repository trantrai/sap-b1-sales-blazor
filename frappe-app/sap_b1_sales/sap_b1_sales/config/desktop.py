"""Desktop config — icon module trên Desk."""

from frappe import _


def get_data():
    return [
        {
            "module_name": "Sales",
            "color": "#12305a",
            "icon": "octicon octicon-file-directory",
            "type": "module",
            "label": _("B1 Sales"),
        },
        {
            "module_name": "B1 Integration",
            "color": "#1d4e89",
            "icon": "octicon octicon-plug",
            "type": "module",
            "label": _("B1 Integration"),
        },
    ]
