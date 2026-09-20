// MasterInventory: all registered item IDs, server-owned paging and grants.
// Set false only if every player should be allowed to grant themselves items.
const MI_OPERATOR_ONLY = true;
const MI_PROJECT = 'masterinventory';
const MI_API = Java.loadClass('com.wysicraft.runtime.api.WysicraftApi');
const MI_COMMANDS = Java.loadClass('net.minecraft.commands.Commands');
const MI_REGISTRY = Java.loadClass('net.minecraft.core.registries.BuiltInRegistries');
const MI_COMPONENTS = Java.loadClass('net.minecraft.core.component.DataComponents');
const MI_BLOCK = Java.loadClass('net.minecraft.world.item.BlockItem');
const MI_ARMOR = Java.loadClass('net.minecraft.world.item.ArmorItem');
const MI_TOOL = Java.loadClass('net.minecraft.world.item.DiggerItem');
const MI_PAGE_SIZE = 20;
const MI_CATEGORIES = ['all','blocks','food','gear','tools','redstone','brewing','misc'];
const MI_LABELS = ['All items','Blocks','Food','Gear','Tools','Redstone','Brewing','Misc'];
global.masterinventory.controller = {
  catalog: null,
  views: {},
  allowed: player => !MI_OPERATOR_ONLY || player.hasPermissions(2),
  view: function(player) {
    const key = String(player.uuid);
    if (!this.views[key]) this.views[key] = {category: 'all', page: 0};
    return this.views[key];
  },
  items: function() {
    if (this.catalog) return this.catalog;
    const groups = {};
    MI_CATEGORIES.forEach(key => groups[key] = []);
    MI_REGISTRY.ITEM.keySet().forEach(key => {
      const id = String(key);
      if (id === 'minecraft:air') return;
      const item = MI_REGISTRY.ITEM.get(key);
      const stack = item.getDefaultInstance();
      let category = 'misc';
      const name = String(stack.getHoverName().getString());
      if (stack.has(MI_COMPONENTS.FOOD)) category = 'food';
      else if (item instanceof MI_ARMOR || /sword|bow$|crossbow|trident|mace$|shield|elytra|horse_armor|wolf_armor/.test(id)) category = 'gear';
      else if (item instanceof MI_TOOL || /pickaxe|_axe$|shovel|_hoe$|shears|fishing_rod|flint_and_steel|brush$|compass|clock$/.test(id)) category = 'tools';
      else if (/redstone|repeater|comparator|piston|observer|hopper|dispenser|dropper|lever|button$|pressure_plate|tripwire|daylight_detector|target$|rail|sculk_sensor|crafter$/.test(id)) category = 'redstone';
      else if (/potion|brewing|blaze_powder|blaze_rod|ghast_tear|magma_cream|fermented_spider_eye|spider_eye|dragon_breath|glistering_melon/.test(id)) category = 'brewing';
      else if (item instanceof MI_BLOCK) category = 'blocks';
      const record = {id: id, name: name};
      groups.all.push(record);
      groups[category].push(record);
    });
    MI_CATEGORIES.forEach(key => groups[key].sort((a,b) => a.name.toLowerCase().localeCompare(b.name.toLowerCase()) || a.id.localeCompare(b.id)));
    this.catalog = groups;
    return groups;
  },
  render: function(ctx) {
    if (!this.allowed(ctx.player)) { ctx.message('MasterInventory requires operator permission.'); ctx.ui.close(); return; }
    const view = this.view(ctx.player);
    const entries = this.items()[view.category];
    const pages = Math.max(1, Math.ceil(entries.length / MI_PAGE_SIZE));
    view.page = Math.max(0, Math.min(view.page, pages - 1));
    const start = view.page * MI_PAGE_SIZE;
    ctx.ui.setText('category_title', MI_LABELS[MI_CATEGORIES.indexOf(view.category)].toUpperCase());
    ctx.ui.setText('catalog_count', entries.length + ' ITEMS');
    ctx.ui.setText('page_status', 'Page ' + (view.page + 1) + ' / ' + pages);
    ctx.ui.setText('status', entries.length ? 'Scroll to browse. Add buttons grant real items.' : 'No registered items in this category.');
    ctx.ui.setEnabled('previous', view.page > 0);
    ctx.ui.setEnabled('next', view.page + 1 < pages);
    MI_CATEGORIES.forEach(key => ctx.ui.setEnabled('tab_' + key, key !== view.category));
    for (let row=0; row<MI_PAGE_SIZE; row++) {
      var entry = entries[start+row];
      ['row_','icon_','name_','id_','add_','ten_'].forEach(prefix => ctx.ui.setVisible(prefix+row, !!entry));
      if (entry) {
        ctx.ui.setItem('icon_'+row, entry.id);
        ctx.ui.setText('name_'+row, entry.name.length > 34 ? entry.name.substring(0,31)+'...' : entry.name);
        ctx.ui.setText('id_'+row, entry.id.length > 44 ? entry.id.substring(0,41)+'...' : entry.id);
      }
    }
  },
  select: function(ctx, category) {
    if (!this.allowed(ctx.player) || MI_CATEGORIES.indexOf(category)<0) return;
    const view=this.view(ctx.player); view.category=category; view.page=0;
    ctx.ui.open('main');
  },
  page: function(ctx, change) {
    if (!this.allowed(ctx.player)) return;
    this.view(ctx.player).page += change;
    ctx.ui.open('main');
  },
  give: function(ctx, row, count) {
    if (!this.allowed(ctx.player)) { ctx.message('MasterInventory requires operator permission.'); return; }
    if (row<0 || row>=MI_PAGE_SIZE || (count!==1 && count!==10)) return;
    const view=this.view(ctx.player);
    const entry=this.items()[view.category][view.page*MI_PAGE_SIZE+row];
    if (!entry) return;
    ctx.player.give(Item.of(entry.id,count));
    ctx.ui.setText('status', 'Added '+count+' x '+entry.name.substring(0,40));
  }
};
MI_API.registerProjectCommand(MI_PROJECT,'mi');
ServerEvents.commandRegistry(event => {
  ['MI','mi'].forEach(name => event.register(MI_COMMANDS.literal(name)
    .requires(source => !MI_OPERATOR_ONLY || source.hasPermission(2))
    .executes(command => {
      const player=command.getSource().getPlayerOrException();
      global.masterinventory.controller.views[String(player.uuid)]={category:'all',page:0};
      MI_API.openProject(player,MI_PROJECT);
      return 1;
    })));
});
function open_inventory(ctx) { global.masterinventory.controller.render(ctx); }


