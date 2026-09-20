// Attach to Load button -> click -> Client. Function: populatePanel
// Rows must already exist: supply_row_1 through supply_row_16,
// with Parent = supplies_panel. All updates replace existing content.
// This JSON string matches test-data.json. No disk/network access is used.
const TEST_JSON = "{\r\n  \"title\": \"Workshop supplies\",\r\n  \"items\": [\r\n    {\r\n      \"quantity\": 8,\r\n      \"name\": \"Oak planks\"\r\n    },\r\n    {\r\n      \"quantity\": 16,\r\n      \"name\": \"Spruce planks\"\r\n    },\r\n    {\r\n      \"quantity\": 24,\r\n      \"name\": \"Iron ingots\"\r\n    },\r\n    {\r\n      \"quantity\": 32,\r\n      \"name\": \"Copper ingots\"\r\n    },\r\n    {\r\n      \"quantity\": 40,\r\n      \"name\": \"Brass sheets\"\r\n    },\r\n    {\r\n      \"quantity\": 48,\r\n      \"name\": \"Andesite alloy\"\r\n    },\r\n    {\r\n      \"quantity\": 56,\r\n      \"name\": \"Shafts\"\r\n    },\r\n    {\r\n      \"quantity\": 64,\r\n      \"name\": \"Cogwheels\"\r\n    },\r\n    {\r\n      \"quantity\": 72,\r\n      \"name\": \"Large cogwheels\"\r\n    },\r\n    {\r\n      \"quantity\": 80,\r\n      \"name\": \"Belts\"\r\n    },\r\n    {\r\n      \"quantity\": 88,\r\n      \"name\": \"Precision mechanisms\"\r\n    },\r\n    {\r\n      \"quantity\": 96,\r\n      \"name\": \"Fuel tanks\"\r\n    }\r\n  ]\r\n}";
function populatePanel(ctx) {
    let data;
    try {
        data = JSON.parse(ctx.state.get("panel_json") || TEST_JSON);
        if (!data || !Array.isArray(data.items)) throw new Error("Expected an items array");
        for (const item of data.items) {
            if (!item || typeof item.name !== "string" || item.name.length > 100 ||
                !Number.isInteger(item.quantity) || item.quantity < 0) {
                throw new Error("Each item needs a name and a non-negative integer quantity");
            }
        }
    } catch (error) {
        console.error("Could not load panel: " + error.message);
        ctx.ui.setText("status", "Invalid JSON: previous list kept");
        return;
    }
    const count = Math.min(data.items.length, 16);
    for (let i = 0; i < 16; i++) {
        const id = "supply_row_" + (i + 1);
        ctx.ui.setText(id, i < count ? data.items[i].name + "  |  Qty: " + data.items[i].quantity : "");
        ctx.ui.setVisible(id, i < count);
    }
    ctx.ui.setText("status", "Loaded " + count + " items" + (data.items.length > 16 ? " (first 16 shown)" : " — scroll inside the panel"));
    console.log("Panel populated from JSON", {shown: count, total: data.items.length});
}