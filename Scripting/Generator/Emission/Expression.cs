﻿using System;
using System.CodeDom;

namespace IronAHK.Scripting
{
    partial class Emit
    {
        bool stmt = false;

        void EmitExpressionStatement(CodeExpression expr)
        {
            stmt = true;
            EmitExpression(expr);
            stmt = false;
        }

        void EmitExpression(CodeExpression expr)
        {
            if (expr is CodeMethodInvokeExpression)
                EmitInvoke((CodeMethodInvokeExpression)expr);
            else if (expr is CodeArrayCreateExpression)
                EmitArray((CodeArrayCreateExpression)expr);
            else if (expr is CodeComplexVariableReferenceExpression)
                EmitComplexReference((CodeComplexVariableReferenceExpression)expr);
            else if (expr is CodePrimitiveExpression)
                EmitPrimitive((CodePrimitiveExpression)expr);
            else if (expr is CodeBinaryOperatorExpression)
                EmitBinary((CodeBinaryOperatorExpression)expr);
            else if (expr is CodeTernaryOperatorExpression)
                EmitTernary((CodeTernaryOperatorExpression)expr);
            else if (expr is CodeVariableReferenceExpression)
                EmitVariableReference((CodeVariableReferenceExpression)expr);
            else if (expr is CodeFieldReferenceExpression)
                EmitFieldReference((CodeFieldReferenceExpression)expr);
            else if (expr is CodeTypeReferenceExpression)
                EmitTypeReference((CodeTypeReferenceExpression)expr);
            else
                throw new ArgumentException("Unrecognised expression: " + expr.GetType().ToString());
        }

        #region Methods

        void EmitInvoke(CodeMethodInvokeExpression invoke)
        {
            if (invoke.Method.TargetObject is CodeTypeReferenceExpression &&
                Type.GetType(((CodeTypeReferenceExpression)invoke.Method.TargetObject).Type.BaseType) == typeof(Script))
            {
                string name = invoke.Method.MethodName;

                if (name == Parser.InternalMethods.LabelCall.MethodName && invoke.Parameters.Count == 1)
                {
                    EmitGoto(new CodeGotoStatement((string)((CodePrimitiveExpression)invoke.Parameters[0]).Value));
                    return;
                }
                else if (name == Parser.InternalMethods.IfElse.MethodName && invoke.Parameters.Count == 1)
                {
                    EmitExpression(invoke.Parameters[0]);
                    return;
                }
                else if (name == Parser.InternalMethods.Operate.MethodName && invoke.Parameters.Count == 3)
                {
                    EmitExpression(invoke.Parameters[1]);
                    writer.Write(Parser.SingleSpace);

                    var op = (Script.Operator)Enum.Parse(typeof(Script.Operator), ((CodeFieldReferenceExpression)invoke.Parameters[0]).FieldName);
                    writer.Write(ScriptOperator(op));

                    writer.Write(Parser.SingleSpace);
                    EmitExpression(invoke.Parameters[2]);
                    return;
                }
                else if (name == Parser.InternalMethods.ExtendArray.MethodName && invoke.Parameters.Count == 1)
                    return;
                else if (name == Parser.InternalMethods.SetObject.MethodName && invoke.Parameters.Count == 4)
                {
                    EmitExpression(invoke.Parameters[1]);
                    EmitExpression(invoke.Parameters[2]);
                    EmitExpression(invoke.Parameters[0]);

                    writer.Write(Parser.SingleSpace);
                    writer.Write(Parser.AssignPre);
                    writer.Write(Parser.Equal);
                    writer.Write(Parser.SingleSpace);

                    EmitExpression(invoke.Parameters[3]);

                    return;
                }
                else if (name == Parser.InternalMethods.Index.MethodName && invoke.Parameters.Count == 2)
                {
                    EmitExpression(invoke.Parameters[0]);
                    writer.Write(Parser.ArrayOpen);
                    EmitExpression(invoke.Parameters[1]);
                    writer.Write(Parser.ArrayClose);

                    return;
                }
                else if (name == Parser.InternalMethods.Dictionary.MethodName && invoke.Parameters.Count == 2)
                {
                    writer.Write(Parser.BlockOpen);
                    writer.Write(Parser.SingleSpace);

                    var parts = new CodeExpressionCollection[2];

                    for (int i = 0; i < parts.Length; i++)
                        parts[i] = ((CodeArrayCreateExpression)invoke.Parameters[i]).Initializers;

                    bool first =true;

                    for (int i = 0; i < parts[0].Count; i++)
                    {
                        if (first)
                            first = false;
                        else
                        {
                            writer.Write(Parser.Multicast);
                            writer.Write(Parser.SingleSpace);
                        }

                        depth++;
                        EmitExpression(parts[0][i]);
                        writer.Write(Parser.SingleSpace);
                        writer.Write(Parser.AssignPre);
                        writer.Write(Parser.SingleSpace);
                        EmitExpression(parts[1][i]);
                        depth--;
                    }

                    writer.Write(Parser.SingleSpace);
                    writer.Write(Parser.BlockClose);

                    return;
                }
            }

            if (invoke.Method.TargetObject != null && 
                !(invoke.Method.TargetObject is CodeTypeReferenceExpression && IsInternalType((CodeTypeReferenceExpression)invoke.Method.TargetObject)))
            {
                depth++;
                EmitExpression(invoke.Method.TargetObject);
                writer.Write(Parser.Concatenate);
                depth--;
            }

            writer.Write(invoke.Method.MethodName);
            writer.Write(Parser.ParenOpen);

            for (int i = 0; i < invoke.Parameters.Count; i++)
            {
                depth++;
                if (i > 0)
                {
                    writer.Write(Parser.Multicast);
                    writer.Write(Parser.SingleSpace);
                }
                EmitExpression(invoke.Parameters[i]);
                depth--;
            }

            writer.Write(Parser.ParenClose);
        }

        void EmitReturn(CodeMethodReturnStatement returns)
        {
            writer.Write(Parser.FlowReturn);

            if (returns.Expression != null)
            {
                writer.Write(Parser.SingleSpace);
                depth++;
                EmitExpression(returns.Expression);
                depth--;
            }
        }

        #endregion

        #region Variables

        void EmitVariableDeclaration(CodeVariableDeclarationStatement var)
        {
            writer.Write(var.Name);
            writer.Write(Parser.SingleSpace);
            writer.Write(Parser.AssignPre);
            writer.Write(Parser.Equal);
            writer.Write(Parser.SingleSpace);

            if (var.InitExpression == null)
                writer.Write(Parser.NullTxt);
            else
            {
                depth++;
                EmitExpression(var.InitExpression);
                depth--;
            }
        }

        void EmitVariableReference(CodeVariableReferenceExpression var)
        {
            writer.Write(var.VariableName);
        }

        void EmitAssignment(CodeAssignStatement assign)
        {
            EmitExpression(assign.Left);
            writer.Write(Parser.SingleSpace);
            writer.Write(Parser.AssignPre);
            writer.Write(Parser.Equal);
            writer.Write(Parser.SingleSpace);
            EmitExpression(assign.Right);
        }

        void EmitArray(CodeArrayCreateExpression array)
        {
            writer.Write(Parser.ArrayOpen);

            bool first = true;
            depth++;
            foreach (CodeExpression expr in array.Initializers)
            {
                if (first)
                    first = false;
                else
                {
                    writer.Write(Parser.Multicast);
                    writer.Write(Parser.SingleSpace);
                }
                EmitExpression(expr);
            }
            depth--;

            writer.Write(Parser.ArrayClose);
        }

        void EmitFieldReference(CodeFieldReferenceExpression field)
        {
            if (field.TargetObject != null)
            {
                depth++;
                EmitExpression(field.TargetObject);
                depth--;
            }

            writer.Write(field.FieldName);
        }

        #region Complex

        void EmitComplexReference(CodeComplexVariableReferenceExpression var)
        {
            var name = var.QualifiedName;

            if (name is CodePrimitiveExpression)
            {
                writer.Write((string)(((CodePrimitiveExpression)name).Value));
            }
            else if (name is CodeArrayCreateExpression)
            {
                var array = (CodeArrayCreateExpression)name;

                foreach (CodeExpression part in array.Initializers)
                {
                    if (part is CodePrimitiveExpression)
                        EmitPrimitive((CodePrimitiveExpression)part);
                    else if (part is CodeComplexVariableReferenceExpression)
                    {
                        writer.Write(Parser.Resolve);
                        EmitComplexReference((CodeComplexVariableReferenceExpression)part);
                        writer.Write(Parser.Resolve);
                    }
                    else
                        throw new ArgumentException("var");
                }
            }
            else
                throw new ArgumentException("var");
        }

        #endregion

        #endregion

        #region Operators

        void EmitBinary(CodeBinaryOperatorExpression binary)
        {
            bool stmt = this.stmt;

            if (stmt)
                this.stmt = false;
            else
                writer.Write(Parser.ParenOpen);

            depth++;
            EmitExpression(binary.Left);
            depth--;

            writer.Write(Parser.SingleSpace);
            writer.Write(Operator(binary.Operator));
            writer.Write(Parser.SingleSpace);

            depth++;
            EmitExpression(binary.Right);
            depth--;

            if (!stmt)
                writer.Write(Parser.ParenClose);
        }

        void EmitTernary(CodeTernaryOperatorExpression ternary)
        {
            depth++;
            EmitExpression(ternary.Condition);
            depth--;
            writer.Write(Parser.SingleSpace);
            writer.Write(Parser.TernaryA);
            writer.Write(Parser.SingleSpace);

            depth++;
            EmitExpression(ternary.TrueBranch);
            depth--;
            writer.Write(Parser.SingleSpace);
            writer.Write(Parser.TernaryB);
            writer.Write(Parser.SingleSpace);

            depth++;
            EmitExpression(ternary.FalseBranch);
            depth--;
        }

        #endregion

        #region Misc

        void EmitPrimitive(CodePrimitiveExpression primitive)
        {
            if (primitive.Value == null)
                writer.Write(Parser.NullTxt);
            else if (primitive.Value is string)
            {
                writer.Write(Parser.StringBound);
                writer.Write((string)primitive.Value);
                writer.Write(Parser.StringBound);
            }
            else if (primitive.Value is decimal)
                writer.Write(((decimal)primitive.Value).ToString());
            else if (primitive.Value is double)
                writer.Write(((double)primitive.Value).ToString());
            else if (primitive.Value is float)
                writer.Write(((float)primitive.Value).ToString());
            else if (primitive.Value is int)
                writer.Write(((int)primitive.Value).ToString());
            else if (primitive.Value is bool)
                writer.Write(((bool)primitive.Value) ? Parser.TrueTxt : Parser.FalseTxt);
            else
                throw new ArgumentException("Unrecognised primitive: " + primitive.Value.ToString());
        }

        void EmitTypeReference(CodeTypeReferenceExpression type)
        {
            if (IsInternalType(type))
                return;

            writer.Write(type.Type.BaseType);
        }

        bool IsInternalType(CodeTypeReferenceExpression type)
        {
            return type.Type.BaseType == typeof(Rusty.Core).FullName;
        }

        #endregion
    }
}