package com.wysicraft.runtime.client;

import com.wysicraft.runtime.model.*;
import com.wysicraft.runtime.model.Models.*;
import net.minecraft.client.gui.GuiGraphics;
import java.util.*;

final class RowTemplateRenderer {
    static boolean active(DynamicScreen s,Element list,Element e,boolean input) {
        var parents=ContainerTree.ancestors(RowTemplates.layout(list),e);
        return e.visible && Expressions.evaluate(e.visibleIf,s.state) && (!input || e.enabled && Expressions.evaluate(e.enabledIf,s.state))
            && parents.stream().allMatch(p->p.visible && Expressions.evaluate(p.visibleIf,s.state) && (!input || p.enabled && Expressions.evaluate(p.enabledIf,s.state)));
    }
    static void draw(DynamicScreen s,GuiGraphics g,Element list,ItemRows.Row row,int index,int x,int y,int width,int height) {
        s.clip(g,x,y,width,height);
        try { for(var source:s.rowDisplay(list,index)) {
            if(!active(s,list,source,false))continue;
            var parents=ContainerTree.ancestors(RowTemplates.layout(list),source);
            for(var p:parents)s.clip(g,x+(int)p.bounds.x,y+(int)p.bounds.y,(int)p.bounds.width,(int)p.bounds.height);
            try {
                var e=source;int ex=x+(int)e.bounds.x,ey=y+(int)e.bounds.y,w=(int)e.bounds.width,h=(int)e.bounds.height;
                ElementRenderers.skin(g,e,ex,ey,w,h);ElementRenderers.get(e.type).draw(s,g,e,ex,ey,w,h,false);ElementRenderers.border(g,e,ex,ey,w,h);
            } finally { for(var p:parents)g.disableScissor(); }
        } } finally {g.disableScissor();}
    }
    static String hit(DynamicScreen s,Element list,double x,double y) {
        var reversed=new ArrayList<>(list.rowElements);Collections.reverse(reversed);
        for(var e:reversed)if(active(s,list,e,true) && inside(e,x,y) && ContainerTree.ancestors(RowTemplates.layout(list),e).stream().allMatch(p->inside(p,x,y))) {
            if(e.type.equals("button"))return e.rowAction;
        }
        return "item_click";
    }
    private static boolean inside(Element e,double x,double y) { return x>=e.bounds.x && y>=e.bounds.y && x<e.bounds.x+e.bounds.width && y<e.bounds.y+e.bounds.height; }
}

