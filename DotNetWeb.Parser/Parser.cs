﻿using Compiler.Core.Expressions;
using DotNetWeb.Core;
using DotNetWeb.Core.Expressions;
using DotNetWeb.Core.Interfaces;
using DotNetWeb.Core.Statements;
using System;
using Type = DotNetWeb.Core.Type;

namespace DotNetWeb.Parser
{
    public class Parser : IParser
    {
        private readonly IScanner scanner;
        private Token lookAhead;
        public Parser(IScanner scanner)
        {
            this.scanner = scanner;
            this.Move();
        }
        public void Parse()
        {
            var program = Program();
            program.ValidateSemantic();
            program.Interpret();
            var code = program.Generate(1);
            System.IO.File.WriteAllText(@"C:\Users\javal\result.html", code);
        }

        private Statement Program()
        {
            EnvironmentManager.PushContext();
            //Init();
            //Template();
            return new SequenceStatement(Init(), Template());
        }

        private Statement Template()
        {
            //Tag();
            //InnerTemplate();
            return new SequenceStatement(Tag(), InnerTemplate());
        }
        
        private Statement InnerTemplate()
        {
            if (this.lookAhead.TokenType == TokenType.LessThan)
            {
                return Template();
            }
            return null;
        }
        private Statement Tag()
        {
            Match(TokenType.LessThan);
            Match(TokenType.Identifier);
            Match(TokenType.GreaterThan);
            var stmt = Stmts();
            Match(TokenType.LessThan);
            Match(TokenType.Slash);
            Match(TokenType.Identifier);
            Match(TokenType.GreaterThan);
            return stmt;
        }

        private Statement Stmts()
        {
            if (this.lookAhead.TokenType == TokenType.OpenBrace)
            {
                //Stmt();
                //Stmts();
                return new SequenceStatement(Stmt(), Stmts());
            }
            return null;
        }

        private Statement Stmt()
        {
            Expression expression;
            Match(TokenType.OpenBrace);
            switch (this.lookAhead.TokenType)
            {
                case TokenType.OpenBrace:
                    Match(TokenType.OpenBrace);
                    expression = Eq();
                    Match(TokenType.CloseBrace);
                    Match(TokenType.CloseBrace);
                    return new AssignationStatement(new Id(expression.Token, expression.type), expression as TypedExpression);
                case TokenType.Percentage:
                    return IfStmt();
                case TokenType.Hyphen:
                    ForeachStatement();
                    break;
                default:
                    throw new ApplicationException("Unrecognized statement");
            }
            return null;
        }

        private void ForeachStatement()
        {
            Match(TokenType.Hyphen);
            Match(TokenType.Percentage);
            Match(TokenType.ForEeachKeyword);
            Match(TokenType.Identifier);
            Match(TokenType.InKeyword);
            Match(TokenType.Identifier);
            Match(TokenType.Percentage);
            Match(TokenType.CloseBrace);
            Template();
            Match(TokenType.OpenBrace);
            Match(TokenType.Percentage);
            Match(TokenType.EndForEachKeyword);
            Match(TokenType.Percentage);
            Match(TokenType.CloseBrace);
        }

        private Statement IfStmt()
        {
            Match(TokenType.Percentage);
            Match(TokenType.IfKeyword);
            var expression = Eq();
            Match(TokenType.Percentage);
            Match(TokenType.CloseBrace);
            var statement = Template();
            Match(TokenType.OpenBrace);
            Match(TokenType.Percentage);
            Match(TokenType.EndIfKeyword);
            Match(TokenType.Percentage);
            Match(TokenType.CloseBrace);
            return new IfStatement(expression as TypedExpression, statement);
        }

        private Expression Eq()
        {
            var expression = Rel();
            while (this.lookAhead.TokenType == TokenType.Equal || this.lookAhead.TokenType == TokenType.NotEqual)
            {
                var token = lookAhead;
                Move();
                expression = new RelationalExpression(token, expression as TypedExpression, Rel() as TypedExpression);
            }
            return expression;
        }

        private Expression Rel()
        {
            var expression = Expr();
            if (this.lookAhead.TokenType == TokenType.LessThan
                || this.lookAhead.TokenType == TokenType.GreaterThan)
            {
                var token = lookAhead;
                Move();
                expression = new RelationalExpression(token, expression as TypedExpression, Expr() as TypedExpression);
            }
            return expression;
        }

        private Expression Expr()
        {
            var expression = Term();
            while (this.lookAhead.TokenType == TokenType.Plus || this.lookAhead.TokenType == TokenType.Hyphen)
            {
                var token = lookAhead;
                Move();
                expression = new ArithmeticOperator(token, expression as TypedExpression, Term() as TypedExpression);
            }
            return expression;
        }

        private Expression Term()
        {
            var expression = Factor();
            while (this.lookAhead.TokenType == TokenType.Asterisk || this.lookAhead.TokenType == TokenType.Slash)
            {
                var token = lookAhead;
                Move();
                expression = new ArithmeticOperator(token, expression as TypedExpression, Factor() as TypedExpression);
            }
            return expression;
        }

        private Expression Factor()
        {
            switch (this.lookAhead.TokenType)
            {
                case TokenType.LeftParens:
                    {
                        Match(TokenType.LeftParens);
                        var expression = Eq();
                        Match(TokenType.RightParens);
                        return expression;
                    }
                case TokenType.IntConstant:
                    var constant = new Constant(lookAhead, Type.Int);
                    Match(TokenType.IntConstant);
                    return constant;
                case TokenType.FloatConstant:
                    constant = new Constant(lookAhead, Type.Float);
                    Match(TokenType.FloatConstant);
                    return constant;
                case TokenType.StringConstant:
                    constant = new Constant(lookAhead, Type.String);
                    Match(TokenType.StringConstant);
                    return constant;
                case TokenType.OpenBracket:
                    Match(TokenType.OpenBracket);
                    ExprList();
                    Match(TokenType.CloseBracket);
                    break;
                default:
                    var symbol = EnvironmentManager.GetSymbol(this.lookAhead.Lexeme);
                    Match(TokenType.Identifier);
                    return symbol.Id;
            }
            return null;
        }

        private Expression ExprList()
        {
            var expression = Eq();
            if (this.lookAhead.TokenType != TokenType.Comma)
            {
                return expression;
            }
            Match(TokenType.Comma);
            return ExprList();
        }

        private Statement Init()
        {
            Match(TokenType.OpenBrace);
            Match(TokenType.Percentage);
            Match(TokenType.InitKeyword);
            var stmts = Code();
            Match(TokenType.Percentage);
            Match(TokenType.CloseBrace);
            return stmts;
        }

        private Statement Code()
        {
            Decls();
            return Assignations();
        }

        private Statement Assignations()
        {
            if (this.lookAhead.TokenType == TokenType.Identifier)
            {
                var symbol = EnvironmentManager.GetSymbol(this.lookAhead.Lexeme);
                return new SequenceStatement(Assignation(symbol.Id), Assignations());
            }
            else
            {
                return null;
            }
        }

        private Statement Assignation(Id id)
        {
            Match(TokenType.Identifier);
            Match(TokenType.Assignation);
            var expression = Eq();
            Match(TokenType.SemiColon);
            return new AssignationStatement(id, expression as TypedExpression);
        }

        private void Decls()
        {
            Decl();
            InnerDecls();
        }

        private void InnerDecls()
        {
            if (this.LookAheadIsType())
            {
                Decls();
            }
        }

        private void Decl()
        {
            switch (this.lookAhead.TokenType)
            {
                case TokenType.FloatKeyword:
                    Match(TokenType.FloatKeyword);
                    var token = lookAhead;
                    Match(TokenType.Identifier);
                    Match(TokenType.SemiColon);
                    var id = new Id(token, Type.Float);
                    EnvironmentManager.AddVariable(token.Lexeme, id);
                    break;
                case TokenType.StringKeyword:
                    Match(TokenType.StringKeyword);
                    token = lookAhead;
                    Match(TokenType.Identifier);
                    Match(TokenType.SemiColon);
                    id = new Id(token, Type.String);
                    EnvironmentManager.AddVariable(token.Lexeme, id);
                    break;
                case TokenType.IntKeyword:
                    Match(TokenType.IntKeyword);
                    token = lookAhead;
                    Match(TokenType.Identifier);
                    Match(TokenType.SemiColon);
                    id = new Id(token, Type.Int);
                    EnvironmentManager.AddVariable(token.Lexeme, id);
                    break;
                case TokenType.FloatListKeyword:
                    Match(TokenType.FloatListKeyword);
                    Match(TokenType.Identifier);
                    Match(TokenType.SemiColon);
                    break;
                case TokenType.IntListKeyword:
                    Match(TokenType.IntListKeyword);
                    Match(TokenType.Identifier);
                    Match(TokenType.SemiColon);
                    break;
                case TokenType.StringListKeyword:
                    Match(TokenType.StringListKeyword);
                    Match(TokenType.Identifier);
                    Match(TokenType.SemiColon);
                    break;
                default:
                    throw new ApplicationException($"Unsupported type {this.lookAhead.Lexeme}");
            }
        }

        private void Move()
        {
            this.lookAhead = this.scanner.GetNextToken();
        }

        private void Match(TokenType tokenType)
        {
            if (this.lookAhead.TokenType != tokenType)
            {
                throw new ApplicationException($"Syntax error! expected token {tokenType} but found {this.lookAhead.TokenType}. Line: {this.lookAhead.Line}, Column: {this.lookAhead.Column}");
            }
            this.Move();
        }

        private bool LookAheadIsType()
        {
            return this.lookAhead.TokenType == TokenType.IntKeyword ||
                this.lookAhead.TokenType == TokenType.StringKeyword ||
                this.lookAhead.TokenType == TokenType.FloatKeyword ||
                this.lookAhead.TokenType == TokenType.IntListKeyword ||
                this.lookAhead.TokenType == TokenType.FloatListKeyword ||
                this.lookAhead.TokenType == TokenType.StringListKeyword;

        }
    }
}
