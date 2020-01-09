parser grammar ParserE7;

options {
	tokenVocab = LexerE7 ;
}

expression: assignment | simpleExpression;

assignment
    : (VAR | LET) ID EQUAL simpleExpression
;

simpleExpression
    : simpleExpression (PLUS | MINUS) simpleExpression
    | simpleExpression (MULTIPLY | DIVIDE) simpleExpression
    | variableRef
    | functionRef
;

variableRef
    : ID
;

functionRef
    : ID OPEN_PAR CLOSE_PAR
;

