package com.wysicraft.runtime.model;
import java.util.*;
import java.util.regex.*;

public final class Expressions {
    private Expressions() {}
    public static String bind(String text, Map<String,String> state) {
        return Pattern.compile("\\$\\{([a-zA-Z_][a-zA-Z0-9_]*)\\}").matcher(text).replaceAll(m -> Matcher.quoteReplacement(state.getOrDefault(m.group(1), "")));
    }
    public static boolean evaluate(String text, Map<String,String> state) {
        if (text.isBlank()) return true;
        if (text.length() > 1024) throw new IllegalArgumentException("Condition too long");
        Parser p = new Parser(text, state); boolean value = p.or(); if (!p.peek().isEmpty()) throw new IllegalArgumentException("Unexpected token"); return value;
    }
    private static class Parser {
        final List<String> tokens = new ArrayList<>(); final Map<String,String> state; int pos;
        Parser(String text, Map<String,String> state) {
            this.state = state;
            Matcher m = Pattern.compile("\\G\\s*(>=|<=|==|!=|&&|\\|\\||[()!<>]|\"[^\"]*\"|'[^']*'|-?\\d+(?:\\.\\d+)?|[a-zA-Z_][a-zA-Z0-9_]*)").matcher(text);
            int offset = 0; while (offset < text.length()) { if (text.substring(offset).isBlank()) break; if (!m.find(offset)) throw new IllegalArgumentException("Invalid condition"); tokens.add(m.group(1)); offset = m.end(); }
        }
        String peek() { return pos < tokens.size() ? tokens.get(pos) : ""; }
        boolean eat(String... choices) { for (String c : choices) if (peek().equals(c)) { pos++; return true; } return false; }
        boolean or() { boolean v = and(); while (eat("OR", "||")) { boolean r = and(); v |= r; } return v; }
        boolean and() { boolean v = unary(); while (eat("AND", "&&")) { boolean r = unary(); v &= r; } return v; }
        boolean unary() {
            if (eat("NOT", "!")) return !unary();
            if (eat("(")) { boolean v = or(); if (!eat(")")) throw new IllegalArgumentException("Missing )"); return v; }
            String left = value(), op = peek();
            if (!eat("==", "!=", ">", "<", ">=", "<=")) { try { return left.equalsIgnoreCase("true") || Double.parseDouble(left) != 0; } catch (NumberFormatException e) { return left.equalsIgnoreCase("true"); } }
            String right = value(); int c;
            try { c = Double.compare(Double.parseDouble(left), Double.parseDouble(right)); } catch (NumberFormatException e) { c = left.compareTo(right); }
            return switch (op) { case "==" -> c == 0; case "!=" -> c != 0; case ">" -> c > 0; case "<" -> c < 0; case ">=" -> c >= 0; default -> c <= 0; };
        }
        String value() { String t = peek(); if (t.isEmpty() || t.equals(")") || t.equals("(")) throw new IllegalArgumentException("Expected value"); pos++; if (t.startsWith("\"") || t.startsWith("'")) return t.substring(1, t.length()-1); return state.getOrDefault(t, t); }
    }
}
