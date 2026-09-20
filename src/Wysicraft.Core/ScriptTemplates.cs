using Wysicraft.Models;
namespace Wysicraft.Core;

public sealed record ScriptTemplate(string Title, bool Server, string Engine, string Body, bool Global = false)
{
    public string Source(string function, string projectId)
    {
        string source = "function " + function + "(ctx) {\n" + Body + "\n}\n";
        if (!Global) return source;
        if (Title.Contains("Open UI command")) return
            "// Edit COMMAND to choose the global command name.\nconst COMMAND = " + Json.Write(projectId + ".menu") + ";\n" +
            "const PROJECT_ID = " + Json.Write(projectId) + ";\n" +
            "const Commands = Java.loadClass('net.minecraft.commands.Commands');\n" +
            "const WysicraftAPI = Java.loadClass('com.wysicraft.runtime.api.WysicraftApi');\n" +
            "WysicraftAPI.registerProjectCommand(PROJECT_ID, COMMAND);\n" +
            "ServerEvents.commandRegistry(event => {\n  event.register(Commands.literal(COMMAND).executes(command => {\n    WysicraftAPI.openProject(command.getSource().getPlayerOrException(), PROJECT_ID);\n    return 1;\n  }));\n});\n\n" + source;
        if (Title.Contains("command alias")) return
            "// A global alias for an existing command; it keeps the player's permissions.\nconst COMMAND = " + Json.Write(projectId + ".portal") + ";\nconst TARGET_COMMAND = 'portal';\n" +
            "const Commands = Java.loadClass('net.minecraft.commands.Commands');\nconst WysicraftAPI = Java.loadClass('com.wysicraft.runtime.api.WysicraftApi');\n" +
            "WysicraftAPI.registerProjectCommand("+Json.Write(projectId)+", COMMAND);\n" +
            "ServerEvents.commandRegistry(event => {\n  event.register(Commands.literal(COMMAND).executes(command => WysicraftAPI.runCommand(command.getSource().getPlayerOrException(), TARGET_COMMAND)));\n});\n\n" + source;
        return "// Edit COMMAND and ITEM_ID. Registration runs when KubeJS loads this file.\n" +
            "const COMMAND = " + Json.Write(projectId + ".reward") + ";\nconst ITEM_ID = 'minecraft:diamond';\nconst COUNT = 1;\n" +
            "const Commands = Java.loadClass('net.minecraft.commands.Commands');\n" +
            "const WysicraftAPI = Java.loadClass('com.wysicraft.runtime.api.WysicraftApi');\n" +
            "WysicraftAPI.registerProjectCommand(" + Json.Write(projectId) + ", COMMAND);\n" +
            "ServerEvents.commandRegistry(event => {\n  event.register(Commands.literal(COMMAND).requires(source => source.hasPermission(2)).executes(command => {\n    return WysicraftAPI.runCommand(command.getSource().getPlayerOrException(), 'give @s ' + ITEM_ID + ' ' + COUNT);\n  }));\n});\n\n" + source;
    }
    public static readonly ScriptTemplate[] All = [
        new("[Template] Give item — Standard Server",true,"standard","  const ITEM_ID = 'minecraft:diamond'; // Replace with your item ID\n  const COUNT = 1; // Replace amount\n  // /give requires the player's command permission.\n  ctx.server.runCommand('give @s ' + ITEM_ID + ' ' + COUNT);"),
        new("[Template] Give item — KubeJS Server",true,"kubejs","  const ITEM_ID = 'minecraft:diamond'; // Replace with your item ID\n  const COUNT = 1;\n  // Trusted server code: add your price/eligibility check here.\n  ctx.player.give(Item.of(ITEM_ID, COUNT));"),
        new("[Template] Run server command",true,"standard","  const COMMAND = 'portal'; // Existing command, without /\n  ctx.server.runCommand(COMMAND);"),
        new("[Template] Populate inventory list",true,"standard","  const LIST_ID = 'inventory'; // Your Item List element ID\n  ctx.ui.setItems(LIST_ID, ctx.player.getInventory());"),
        new("[Template] Open project",true,"standard","  const PROJECT_ID = '__PROJECT__';\n  ctx.server.runCommand(PROJECT_ID + '.open');"),
        new("[Template] Global reward command — KubeJS",true,"kubejs","  // Registered during script loading. This assignment does not grant an item on screen open.\n  // To invoke from a button: ctx.runCommand(COMMAND);",true),
        new("[Template] Global Open UI command — KubeJS",true,"kubejs","  // Registration happens on script loading; no action needed when this screen opens.\n  // Other scripts and buttons can run the command named above.",true),
        new("[Template] Global command alias — KubeJS",true,"kubejs","  // Registration only. Set TARGET_COMMAND to a command supplied by the server or another mod.",true),
        new("[Template] Teleport — Standard Server",true,"standard","  const X = 0, Y = 80, Z = 0; // Destination; /tp requires permission\n  ctx.server.runCommand('tp @s ' + X + ' ' + Y + ' ' + Z);"),
        new("[Template] Message player — Standard Server",true,"standard","  const MESSAGE = 'Welcome!';\n  ctx.message(MESSAGE);"),
        new("[Template] Change text",false,"standard","  const ELEMENT_ID = 'status'; // Replace element ID\n  const TEXT = 'Hello!';\n  ctx.ui.setText(ELEMENT_ID, TEXT);"),
        new("[Template] Close screen",false,"standard","  ctx.ui.close();")
    ];
}
