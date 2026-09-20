JSON Scroll Panel Demo

1. Open project.json or ../json_scroll_demo.wysicraft in the updated designer.
2. Open Preview, click Load test JSON, then scroll with the mouse wheel over the panel.

To use in your UI:
- Create a scroll_panel with ID supplies_panel.
- Create 16 labels named supply_row_1 through supply_row_16 and set Parent to supplies_panel.
- Place rows 28 pixels apart using screen coordinates; rows can extend below the panel.
- Create a status label with ID status.
- Import scripts/client/populate_panel.js and attach populatePanel to your button's click Client handler.
- Or paste the entire file into New Script, set Function to populatePanel, and Save & Assign.

The standalone test-data.json shows the fixture. The same JSON is embedded in TEST_JSON in the script so the current sandbox can read it without filesystem access. Editing the standalone file alone does not update the script: replace TEST_JSON too. Alternatively, set the screen variable panel_json to a JSON string before clicking. An empty variable uses TEST_JSON.

Expected format: {"items":[{"name":"Oak planks","quantity":8}]}
At most 16 rows are displayed. Unused rows are hidden; repeated clicks overwrite rows. Invalid JSON preserves the previous list and reports an error. To raise capacity, add matching labels and change the script's 16 limits.

This example uses existing row elements, not dynamic element creation. It runs in desktop Preview. It also runs in Minecraft with the updated runtime JAR, which bundles the client JavaScript engine. Open this project, choose Minecraft test, leave the skip checkbox unchecked, press Start test, then .open and click Load supplies.
