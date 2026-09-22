package com.wysicraft.runtime.model;
import com.wysicraft.runtime.model.Models.*;
import java.util.*;

public final class ResponsiveLayout {
    public static Map<String,Bounds> resolve(Ui design,double width,double height) {
        var result=new HashMap<String,Bounds>();var visiting=new HashSet<String>();
        class Resolver {
            Bounds place(Element e) {
                if(result.containsKey(e.id))return result.get(e.id);
                if(!visiting.add(e.id))throw new IllegalArgumentException("Container cycle");
                var parent=design.element(e.parent);var old=new Bounds();old.width=design.size.width;old.height=design.size.height;
                var next=new Bounds();next.width=width;next.height=height;
                if(parent!=null){old=parent.bounds;next=place(parent);}
                double dw=next.width-old.width,dh=next.height-old.height;
                var b=new Bounds();b.x=e.bounds.x+next.x-old.x;b.y=e.bounds.y+next.y-old.y;b.width=e.bounds.width;b.height=e.bounds.height;
                switch(e.horizontalAnchor){case "right"->b.x+=dw;case "center"->b.x+=dw/2;case "stretch"->b.width=Math.max(e.minWidth,b.width+dw);}
                switch(e.verticalAnchor){case "bottom"->b.y+=dh;case "center"->b.y+=dh/2;case "stretch"->b.height=Math.max(e.minHeight,b.height+dh);}
                visiting.remove(e.id);result.put(e.id,b);return b;
            }
        }
        var resolver=new Resolver();for(var e:design.elements)resolver.place(e);return result;
    }
    public static void apply(Ui target,Ui design,int width,int height) {
        width=design.responsive?Math.clamp(width,16,4096):design.size.width;height=design.responsive?Math.clamp(height,16,4096):design.size.height;
        var bounds=resolve(design,width,height);
        for(var e:target.elements) {
            e.bounds=bounds.get(e.id);var source=design.element(e.id);
            if(!source.rowElements.isEmpty()) {
                var row=Models.JSON.fromJson(Models.JSON.toJson(source),Element.class);var layout=RowTemplates.layout(source);
                layout.size.width=(int)(source.rowTemplateWidth>0?source.rowTemplateWidth:source.bounds.width);layout.size.height=source.rowHeight;
                var rowBounds=resolve(layout,e.bounds.width,e.rowHeight);
                for(var child:row.rowElements)child.bounds=rowBounds.get(child.id);e.rowElements=row.rowElements;
            }
        }
        target.size.width=width;target.size.height=height;
    }
}
