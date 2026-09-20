// OPTIONAL: install in kubejs/server_scripts/. Requires KubeJS 2101 on the server.
// Right-click a gold block to open the main UI for that player.
const FieldkitUI = Java.loadClass('com.wysicraft.runtime.api.WysicraftApi');
BlockEvents.rightClicked(event => {
    if (event.block.id === 'minecraft:gold_block') {
        FieldkitUI.openProject(event.player, 'live_inventory');
        event.cancel();
    }
});
