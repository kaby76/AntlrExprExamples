grammar E4;

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

// Insert here...
finally { int x = 0; }

VAR: [vV] [aA] [rR];
LET: [lL] [eE] [tT];

PLUS: '+';
MINUS: '-';
MULTIPLY: '*';
DIVIDE: '/';
EQUAL: '=';
OPEN_PAR: '(';
CLOSE_PAR: ')';
ID: [a-zA-Z] [a-zA-Z0-9_]*;
WS: [ \n\r\t] -> channel(HIDDEN);