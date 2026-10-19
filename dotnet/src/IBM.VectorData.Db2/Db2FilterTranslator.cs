// Copyright (c) IBM Corporation. All rights reserved.

// Adapted from SqlFilterTranslator / SqlServerFilterTranslator in CommunityToolkit/AI
// (https://github.com/CommunityToolkit/AI, MIT License, Copyright .NET Foundation and Contributors).

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.Extensions.VectorData.ProviderServices;
using Microsoft.Extensions.VectorData.ProviderServices.Filter;

namespace IBM.VectorData.Db2;

/// <summary>
/// Translates a LINQ filter into a Db2 search condition. Captured values become <c>?</c> parameter markers,
/// collected in <see cref="ParameterValues"/> in the order they appear in the SQL.
/// </summary>
internal sealed class Db2FilterTranslator : FilterTranslatorBase
{
    private readonly StringBuilder _sql;
    private readonly Expression _preprocessedExpression;

    internal Db2FilterTranslator(CollectionModel model, LambdaExpression lambdaExpression, StringBuilder sql)
    {
        Debug.Assert(lambdaExpression.Parameters.Count == 1);
        this._sql = sql;

        this._preprocessedExpression = this.PreprocessFilter(lambdaExpression, model, new FilterPreprocessingOptions { SupportsParameterization = true });
    }

    internal List<object?> ParameterValues { get; } = [];

    internal void Translate() => this.Translate(this._preprocessedExpression, isSearchCondition: true);

    private void Translate(Expression? node, bool isSearchCondition = false)
    {
        switch (node)
        {
            case BinaryExpression binary:
                this.TranslateBinary(binary);
                return;

            case ConstantExpression constant:
                this.TranslateConstant(constant.Value, isSearchCondition);
                return;

            case QueryParameterExpression { Value: var value }:
                this.TranslateQueryParameter(value);
                return;

            case MemberExpression member:
                this.TranslateMember(member, isSearchCondition);
                return;

            case MethodCallExpression methodCall:
                this.TranslateMethodCall(methodCall, isSearchCondition);
                return;

            case UnaryExpression unary:
                this.TranslateUnary(unary, isSearchCondition);
                return;

            default:
                throw new NotSupportedException("Unsupported NodeType in filter: " + node?.NodeType);
        }
    }

    private void TranslateBinary(BinaryExpression binary)
    {
        // Comparisons with null need IS [NOT] NULL.
        switch (binary.NodeType)
        {
            case ExpressionType.Equal when IsNull(binary.Right):
                this._sql.Append('(');
                this.Translate(binary.Left);
                this._sql.Append(" IS NULL)");
                return;
            case ExpressionType.NotEqual when IsNull(binary.Right):
                this._sql.Append('(');
                this.Translate(binary.Left);
                this._sql.Append(" IS NOT NULL)");
                return;
            case ExpressionType.Equal when IsNull(binary.Left):
                this._sql.Append('(');
                this.Translate(binary.Right);
                this._sql.Append(" IS NULL)");
                return;
            case ExpressionType.NotEqual when IsNull(binary.Left):
                this._sql.Append('(');
                this.Translate(binary.Right);
                this._sql.Append(" IS NOT NULL)");
                return;
        }

        bool isLogical = binary.NodeType is ExpressionType.AndAlso or ExpressionType.OrElse;

        this._sql.Append('(');
        this.Translate(binary.Left, isSearchCondition: isLogical);

        this._sql.Append(binary.NodeType switch
        {
            ExpressionType.Equal => " = ",
            ExpressionType.NotEqual => " <> ",

            ExpressionType.GreaterThan => " > ",
            ExpressionType.GreaterThanOrEqual => " >= ",
            ExpressionType.LessThan => " < ",
            ExpressionType.LessThanOrEqual => " <= ",

            ExpressionType.AndAlso => " AND ",
            ExpressionType.OrElse => " OR ",

            _ => throw new NotSupportedException("Unsupported binary expression node type: " + binary.NodeType),
        });

        this.Translate(binary.Right, isSearchCondition: isLogical);
        this._sql.Append(')');

        static bool IsNull(Expression expression)
            => expression is ConstantExpression { Value: null } or QueryParameterExpression { Value: null };
    }

    private void TranslateConstant(object? value, bool isSearchCondition)
    {
        switch (value)
        {
            case bool b when isSearchCondition:
                this._sql.Append(b ? "1 = 1" : "1 = 0");
                return;
            case bool b:
                this._sql.Append(b ? "TRUE" : "FALSE");
                return;

            case short or int or long or decimal:
                this._sql.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            case float f:
                this._sql.Append(f.ToString("R", CultureInfo.InvariantCulture)).Append("E0");
                return;
            case double d:
                this._sql.Append(d.ToString("R", CultureInfo.InvariantCulture)).Append("E0");
                return;

            case string untrustedInput:
                // The only place where untrusted input is inlined, so it must be quoted and escaped.
                this._sql.Append('\'').Append(untrustedInput.Replace("'", "''")).Append('\'');
                return;
            case Guid g:
                this._sql.Append('\'').Append(g.ToString("D")).Append('\'');
                return;

            // Arrays are stored as canonical JSON, so comparing the JSON text compares the arrays.
            case string[] strings:
                this.TranslateConstant(Db2Json.Serialize(strings), isSearchCondition);
                return;
            case List<string> strings:
                this.TranslateConstant(Db2Json.Serialize(strings), isSearchCondition);
                return;

            case DateTime dateTime:
                this._sql.Append("TIMESTAMP '").Append(dateTime.ToString("yyyy-MM-dd HH:mm:ss.fffffff", CultureInfo.InvariantCulture)).Append('\'');
                return;
            case DateOnly date:
                this._sql.Append("DATE '").Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\'');
                return;
            case TimeOnly time:
                this._sql.Append("TIME '").Append(time.ToString("HH:mm:ss", CultureInfo.InvariantCulture)).Append('\'');
                return;

            case null:
                this._sql.Append("NULL");
                return;

            default:
                throw new NotSupportedException("Unsupported constant type: " + value.GetType().Name);
        }
    }

    private void TranslateQueryParameter(object? value)
    {
        // Equality with NULL needs different SQL anyway (IS NULL), and an untyped NULL parameter is not allowed here.
        if (value is null)
        {
            this._sql.Append("NULL");
            return;
        }

        this.ParameterValues.Add(value);
        this._sql.Append('?');
    }

    private void TranslateMember(MemberExpression memberExpression, bool isSearchCondition)
    {
        if (this.TryBindProperty(memberExpression, out var property))
        {
            this.GenerateColumn(property, isSearchCondition);
            return;
        }

        throw new NotSupportedException($"Member access for '{memberExpression.Member.Name}' is unsupported - only member access over the filter parameter are supported");
    }

    private void GenerateColumn(PropertyModel property, bool isSearchCondition = false)
    {
        this._sql.AppendIdentifier(property.StorageName);

        // A bare boolean column used as a condition, e.g. r => r.IsActive
        if (isSearchCondition)
        {
            this._sql.Append(" = TRUE");
        }
    }

    private void TranslateMethodCall(MethodCallExpression methodCall, bool isSearchCondition = false)
    {
        // Dictionary access for dynamic mapping (r => r["SomeString"] == "foo")
        if (this.TryBindProperty(methodCall, out var property))
        {
            this.GenerateColumn(property, isSearchCondition);
            return;
        }

        if (TryMatchContains(methodCall, out var source, out var item))
        {
            this.TranslateContains(source, item);
            return;
        }

        // r => r.Strings.Any(s => values.Contains(s))
        if (methodCall is { Method.Name: nameof(Enumerable.Any), Arguments: [var anySource, LambdaExpression lambda] }
            && methodCall.Method.DeclaringType == typeof(Enumerable))
        {
            this.TranslateAny(anySource, lambda);
            return;
        }

        throw new NotSupportedException($"Unsupported method call: {methodCall.Method.DeclaringType?.Name}.{methodCall.Method.Name}");
    }

    private void TranslateContains(Expression source, Expression item)
    {
        switch (source)
        {
            // Contains over an array column (r => r.Strings.Contains("foo"))
            case var _ when this.TryBindProperty(source, out var property):
                this.TranslateArrayColumnContains(property, [GetStringValue(item)]);
                return;

            // Contains over inline array (r => new[] { "foo", "bar" }.Contains(r.String))
            case NewArrayExpression newArray:
                this.TranslateIn(item, newArray.Expressions, element => this.Translate(element));
                return;

            // Contains over captured array (r => arrayLocalVariable.Contains(r.String))
            case QueryParameterExpression { Value: IEnumerable elements }:
                this.TranslateIn(item, elements.Cast<object?>(), element => this.TranslateConstant(element, isSearchCondition: false));
                return;

            default:
                throw new NotSupportedException("Unsupported Contains expression");
        }
    }

    private void TranslateIn<T>(Expression item, IEnumerable<T> elements, Action<T> translateElement)
    {
        bool isFirst = true;
        foreach (var element in elements)
        {
            if (isFirst)
            {
                this._sql.Append('(');
                this.Translate(item);
                this._sql.Append(" IN (");
                isFirst = false;
            }
            else
            {
                this._sql.Append(", ");
            }

            translateElement(element);
        }

        // An empty IN list is not valid SQL; nothing can be contained in an empty list.
        this._sql.Append(isFirst ? "1 = 0" : "))");
    }

    private void TranslateAny(Expression source, LambdaExpression lambda)
    {
        // Only r.ArrayColumn.Any(x => values.Contains(x)) is supported, with inline or captured values.
        if (!this.TryBindProperty(source, out var property)
            || lambda.Body is not MethodCallExpression containsCall
            || !TryMatchContains(containsCall, out var valuesExpression, out var itemExpression)
            || itemExpression != lambda.Parameters[0])
        {
            throw new NotSupportedException("Unsupported method call: Enumerable.Any");
        }

        IEnumerable<string> values = valuesExpression switch
        {
            NewArrayExpression newArray => newArray.Expressions.Select(GetStringValue),
            QueryParameterExpression { Value: IEnumerable<string> captured } => captured,
            ConstantExpression { Value: IEnumerable<string> constant } => constant,
            _ => throw new NotSupportedException("Unsupported method call: Enumerable.Any"),
        };

        this.TranslateArrayColumnContains(property, values);
    }

    /// <summary>
    /// Matches rows whose JSON array column contains any of the values.
    /// </summary>
    /// <remarks>
    /// Arrays are stored as compact JSON, e.g. <c>["a","b"]</c>. Stripping the brackets and wrapping the text in commas
    /// gives <c>,"a","b",</c>, in which every element appears as <c>,&lt;serialized element&gt;,</c>. An unescaped quote
    /// always delimits an element, so the search cannot match inside another element.
    /// </remarks>
    private void TranslateArrayColumnContains(PropertyModel property, IEnumerable<string> values)
    {
        bool isFirst = true;
        foreach (string value in values)
        {
            this._sql.Append(isFirst ? "((" : " OR (");
            isFirst = false;

            this._sql.Append("LOCATE(',' || CAST(? AS VARCHAR(").Append(Db2Constants.MaxStringLength).Append(")) || ',', ',' || SUBSTR(");
            this._sql.AppendIdentifier(property.StorageName).Append(", 2, LENGTH(").AppendIdentifier(property.StorageName).Append(") - 2) || ',') > 0)");
            this.ParameterValues.Add(Db2Json.Serialize(value));
        }

        // Nothing can be contained in an empty list.
        this._sql.Append(isFirst ? "1 = 0" : ")");
    }

    private static string GetStringValue(Expression expression)
        => expression switch
        {
            ConstantExpression { Value: string value } => value,
            QueryParameterExpression { Value: string value } => value,
            _ => throw new NotSupportedException("Contains over an array column requires a non-null string value."),
        };

    private void TranslateUnary(UnaryExpression unary, bool isSearchCondition)
    {
        switch (unary.NodeType)
        {
            case ExpressionType.Not:
                // !(a == b) and !(a != b) become a != b and a == b, which keeps the null handling above.
                if (unary.Operand is BinaryExpression { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } binary)
                {
                    this.TranslateBinary(
                        Expression.MakeBinary(
                            binary.NodeType is ExpressionType.Equal ? ExpressionType.NotEqual : ExpressionType.Equal,
                            binary.Left,
                            binary.Right));
                    return;
                }

                this._sql.Append("(NOT ");
                this.Translate(unary.Operand, isSearchCondition: true);
                this._sql.Append(')');
                return;

            // Converting non-nullable to nullable, e.g. r => r.Int == nullableInt
            case ExpressionType.Convert when Nullable.GetUnderlyingType(unary.Type) == unary.Operand.Type:
                this.Translate(unary.Operand, isSearchCondition);
                return;

            // Convert over dictionary access for dynamic mapping (r => (int)r["SomeInt"] == 8)
            case ExpressionType.Convert when this.TryBindProperty(unary.Operand, out var property) && unary.Type == property.Type:
                this.GenerateColumn(property, isSearchCondition);
                return;

            default:
                throw new NotSupportedException("Unsupported unary expression node type: " + unary.NodeType);
        }
    }
}
