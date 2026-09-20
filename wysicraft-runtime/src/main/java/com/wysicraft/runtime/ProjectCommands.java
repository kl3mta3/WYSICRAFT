package com.wysicraft.runtime;

import com.mojang.brigadier.CommandDispatcher;
import com.mojang.brigadier.tree.CommandNode;
import com.wysicraft.runtime.pack.PackRepository;
import net.minecraft.commands.CommandSourceStack;
import net.minecraft.commands.Commands;
import net.minecraft.commands.arguments.EntityArgument;
import net.minecraft.network.chat.Component;
import net.minecraft.server.level.ServerPlayer;
import java.util.*;

/** Project entry points resolve the current manifest on every invocation, including after reload. */
public final class ProjectCommands {
    private ProjectCommands() {}
    private static final Map<CommandDispatcher<CommandSourceStack>, Map<String,CommandNode<CommandSourceStack>>> OWNED = new WeakHashMap<>();
    private static final Map<String,Set<String>> ASSOCIATED = new java.util.concurrent.ConcurrentHashMap<>();
    public static void associate(String project, String command) {
        if (!PackRepository.id(project) || !command.matches("[a-z0-9_.:-]{1,128}")) throw new IllegalArgumentException("Use a project ID and command root without slash/arguments");
        ASSOCIATED.computeIfAbsent(project, key -> java.util.concurrent.ConcurrentHashMap.newKeySet()).add(command);
    }
    public static List<String> available(CommandDispatcher<CommandSourceStack> dispatcher, CommandSourceStack source, String project) {
        return dispatcher.getRoot().getChildren().stream().filter(node -> node.canUse(source) && (node.getName().startsWith(project + ".") || ASSOCIATED.getOrDefault(project,Set.of()).contains(node.getName())))
            .map(CommandNode::getName).sorted().toList();
    }
    public static void register(CommandDispatcher<CommandSourceStack> dispatcher) {
        var owned = OWNED.computeIfAbsent(dispatcher, key -> new HashMap<>());
        for (var pack : new HashSet<>(Wysicraft.SERVER.packs.all().values())) {
            String id = pack.manifest().id;
            for (boolean opening : new boolean[] { true, false }) {
                String name = id + (opening ? ".open" : ".close");
                var existing = dispatcher.getRoot().getChild(name);
                if (existing != null) {
                    if (existing != owned.get(name)) Wysicraft.LOG.warn("Cannot register /{}: another command already uses that name", name);
                    continue;
                }
                var node = dispatcher.register(Commands.literal(name)
                    .executes(ctx -> execute(ctx.getSource(),ctx.getSource().getPlayerOrException(),id,opening))
                    .then(Commands.argument("player",EntityArgument.player()).requires(source -> source.hasPermission(2))
                        .executes(ctx -> execute(ctx.getSource(),EntityArgument.getPlayer(ctx,"player"),id,opening))));
                owned.put(name,node);
            }
        }
    }
    private static int execute(CommandSourceStack source, ServerPlayer player, String id, boolean opening) {
        PackRepository.Loaded pack = Wysicraft.SERVER.packs.all().values().stream().filter(p -> p.manifest().id.equals(id)).findFirst().orElse(null);
        if (pack == null) { source.sendFailure(Component.literal("WYSICRAFT project is not loaded: " + id)); return 0; }
        if (opening) Wysicraft.SERVER.open(player,id+":"+pack.manifest().defaultUi);
        else Wysicraft.SERVER.closeProject(player,id);
        return 1;
    }
}
