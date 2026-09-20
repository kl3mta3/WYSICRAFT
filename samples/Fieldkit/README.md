# Fieldkit

Open project.json in the updated designer. Export and install with the updated runtime, or use Minecraft test. `/fieldkit.open` opens Food; Gear, Blocks and Loot are tabs. Refresh changes all twelve displayed item IDs and generates quantities. Items within a tab are distinct. Nothing is granted to the player.

The refresh script now uses `ctx.ui.setItem('icon_0', 'minecraft:diamond')`, with one item control per slot. It no longer switches visibility between candidate icons. Each refresh uses 41 script output operations instead of 77. Amount labels are separate from item controls.

The desktop preview shows item placeholders with updated names; Minecraft renders native icons. Installed mod IDs can be added to the POOLS data. Unknown in-game item IDs produce a script error and leave the affected icon unchanged.

Restart the app/game with the updated designer/runtime before testing. Older versions do not expose setItem. The local MCP endpoint/token changes when restarting the app.
