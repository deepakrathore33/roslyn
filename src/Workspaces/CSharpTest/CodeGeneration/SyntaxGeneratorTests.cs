﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Editing;

using static CSharpSyntaxTokens;
using static SyntaxFactory;

[UseExportProvider]
public sealed class SyntaxGeneratorTests
{
    private readonly CSharpCompilation _emptyCompilation = CSharpCompilation.Create("empty",
        references: [NetFramework.mscorlib, NetFramework.System]);

    private Workspace _workspace;
    private SyntaxGenerator _generator;

    private Workspace Workspace
        => _workspace ??= new AdhocWorkspace();

    private SyntaxGenerator Generator
        => _generator ??= SyntaxGenerator.GetGenerator(Workspace, LanguageNames.CSharp);

    public static Compilation Compile(string code)
    {
        return CSharpCompilation.Create("test")
            .AddReferences(NetFramework.mscorlib, NetFramework.System, NetFramework.SystemCore, NetFramework.SystemRuntime, NetFramework.SystemValueTuple)
            .AddSyntaxTrees(ParseSyntaxTree(code));
    }

    private static void VerifySyntax<TSyntax>(SyntaxNode node, string expectedText) where TSyntax : SyntaxNode
    {
        Assert.IsAssignableFrom<TSyntax>(node);
        var normalized = node.NormalizeWhitespace().ToFullString();
        AssertEx.Equal(expectedText, normalized);
    }

    private static void VerifySyntaxRaw<TSyntax>(SyntaxNode node, string expectedText) where TSyntax : SyntaxNode
    {
        Assert.IsAssignableFrom<TSyntax>(node);
        var normalized = node.ToFullString();
        Assert.Equal(expectedText, normalized);
    }

    #region Expressions and Statements
    [Fact]
    public void TestLiteralExpressions()
    {
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0), "0");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1), "1");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression(-1), "-1");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(int.MinValue), "global::System.Int32.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(int.MaxValue), "global::System.Int32.MaxValue");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0L), "0L");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1L), "1L");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression(-1L), "-1L");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(long.MinValue), "global::System.Int64.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(long.MaxValue), "global::System.Int64.MaxValue");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0UL), "0UL");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1UL), "1UL");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(ulong.MinValue), "0UL");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(ulong.MaxValue), "global::System.UInt64.MaxValue");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0.0f), "0F");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1.0f), "1F");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression(-1.0f), "-1F");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(float.MinValue), "global::System.Single.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(float.MaxValue), "global::System.Single.MaxValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(float.Epsilon), "global::System.Single.Epsilon");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(float.NaN), "global::System.Single.NaN");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(float.NegativeInfinity), "global::System.Single.NegativeInfinity");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(float.PositiveInfinity), "global::System.Single.PositiveInfinity");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0.0), "0D");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1.0), "1D");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression(-1.0), "-1D");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(double.MinValue), "global::System.Double.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(double.MaxValue), "global::System.Double.MaxValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(double.Epsilon), "global::System.Double.Epsilon");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(double.NaN), "global::System.Double.NaN");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(double.NegativeInfinity), "global::System.Double.NegativeInfinity");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(double.PositiveInfinity), "global::System.Double.PositiveInfinity");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0m), "0M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0.00m), "0.00M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1.00m), "1.00M");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression(-1.00m), "-1.00M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1.0000000000m), "1.0000000000M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0.000000m), "0.000000M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0.0000000m), "0.0000000M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1000000000m), "1000000000M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(123456789.123456789m), "123456789.123456789M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1E-28m), "0.0000000000000000000000000001M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(0E-28m), "0.0000000000000000000000000000M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(1E-29m), "0.0000000000000000000000000000M");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(-1E-29m), "0.0000000000000000000000000000M");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(decimal.MinValue), "global::System.Decimal.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(decimal.MaxValue), "global::System.Decimal.MaxValue");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression('c'), "'c'");

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression("str"), """
            "str"
            """);
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression("s\"t\"r"), """
            "s\"t\"r"
            """);

        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(true), "true");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(false), "false");
    }

    [Fact]
    public void TestShortLiteralExpressions()
    {
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((short)0), "0");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((short)1), "1");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression((short)-1), "-1");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(short.MinValue), "global::System.Int16.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(short.MaxValue), "global::System.Int16.MaxValue");
    }

    [Fact]
    public void TestUshortLiteralExpressions()
    {
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((ushort)0), "0");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((ushort)1), "1");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(ushort.MinValue), "0");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(ushort.MaxValue), "global::System.UInt16.MaxValue");
    }

    [Fact]
    public void TestSbyteLiteralExpressions()
    {
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((sbyte)0), "0");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((sbyte)1), "1");
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LiteralExpression((sbyte)-1), "-1");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(sbyte.MinValue), "global::System.SByte.MinValue");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.LiteralExpression(sbyte.MaxValue), "global::System.SByte.MaxValue");
    }

    [Fact]
    public void TestByteLiteralExpressions()
    {
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((byte)0), "0");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression((byte)1), "1");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(byte.MinValue), "0");
        VerifySyntax<LiteralExpressionSyntax>(Generator.LiteralExpression(byte.MaxValue), "255");
    }

    [Fact]
    public void TestAttributeData()
    {
        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { }
            """,
            @"[MyAttribute]")),
@"[global::MyAttribute]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public MyAttribute(object value) { } }
            """,
            @"[MyAttribute(null)]")),
@"[global::MyAttribute(null)]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public MyAttribute(int value) { } }
            """,
            @"[MyAttribute(123)]")),
@"[global::MyAttribute(123)]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public MyAttribute(double value) { } }
            """,
            @"[MyAttribute(12.3)]")),
@"[global::MyAttribute(12.3)]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public MyAttribute(string value) { } }
            """,
            @"[MyAttribute(""value"")]")),
@"[global::MyAttribute(""value"")]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public enum E { A, B, C }
            public class MyAttribute : Attribute { public MyAttribute(E value) { } }
            """,
            @"[MyAttribute(E.A)]")),
@"[global::MyAttribute(global::E.A)]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public MyAttribute(Type value) { } }
            """,
            @"[MyAttribute(typeof (MyAttribute))]")),
@"[global::MyAttribute(typeof(global::MyAttribute))]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public MyAttribute(int[] values) { } }
            """,
            @"[MyAttribute(new [] {1, 2, 3})]")),
@"[global::MyAttribute(new[] { 1, 2, 3 })]");

        VerifySyntax<AttributeListSyntax>(Generator.Attribute(GetAttributeData(
            """
            using System; 
            public class MyAttribute : Attribute { public int Value {get; set;} }
            """,
            @"[MyAttribute(Value = 123)]")),
@"[global::MyAttribute(Value = 123)]");

        var attributes = Generator.GetAttributes(Generator.AddAttributes(
            Generator.NamespaceDeclaration("n"),
            Generator.Attribute("Attr")));
        Assert.Single(attributes);
    }

    private static AttributeData GetAttributeData(string decl, string use)
    {
        var compilation = Compile(decl + "\r\n" + use + "\r\nclass C { }");
        var typeC = compilation.GlobalNamespace.GetMembers("C").First() as INamedTypeSymbol;
        return typeC.GetAttributes().First();
    }

    [Fact]
    public void TestNameExpressions()
    {
        VerifySyntax<IdentifierNameSyntax>(Generator.IdentifierName("x"), "x");
        VerifySyntax<QualifiedNameSyntax>(Generator.QualifiedName(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "x.y");
        VerifySyntax<QualifiedNameSyntax>(Generator.DottedName("x.y"), "x.y");

        VerifySyntax<GenericNameSyntax>(Generator.GenericName("x", Generator.IdentifierName("y")), "x<y>");
        VerifySyntax<GenericNameSyntax>(Generator.GenericName("x", Generator.IdentifierName("y"), Generator.IdentifierName("z")), "x<y, z>");

        // convert identifier name into generic name
        VerifySyntax<GenericNameSyntax>(Generator.WithTypeArguments(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "x<y>");

        // convert qualified name into qualified generic name
        VerifySyntax<QualifiedNameSyntax>(Generator.WithTypeArguments(Generator.DottedName("x.y"), Generator.IdentifierName("z")), "x.y<z>");

        // convert member access expression into generic member access expression
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.WithTypeArguments(Generator.MemberAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")), "x.y<z>");

        // convert existing generic name into a different generic name
        var gname = Generator.WithTypeArguments(Generator.IdentifierName("x"), Generator.IdentifierName("y"));
        VerifySyntax<GenericNameSyntax>(gname, "x<y>");
        VerifySyntax<GenericNameSyntax>(Generator.WithTypeArguments(gname, Generator.IdentifierName("z")), "x<z>");
    }

    [Fact]
    public void TestTypeExpressions()
    {
        // these are all type syntax too
        VerifySyntax<TypeSyntax>(Generator.IdentifierName("x"), "x");
        VerifySyntax<TypeSyntax>(Generator.QualifiedName(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "x.y");
        VerifySyntax<TypeSyntax>(Generator.DottedName("x.y"), "x.y");
        VerifySyntax<TypeSyntax>(Generator.GenericName("x", Generator.IdentifierName("y")), "x<y>");
        VerifySyntax<TypeSyntax>(Generator.GenericName("x", Generator.IdentifierName("y"), Generator.IdentifierName("z")), "x<y, z>");

        VerifySyntax<TypeSyntax>(Generator.ArrayTypeExpression(Generator.IdentifierName("x")), "x[]");
        VerifySyntax<TypeSyntax>(Generator.ArrayTypeExpression(Generator.ArrayTypeExpression(Generator.IdentifierName("x"))), "x[][]");
        VerifySyntax<TypeSyntax>(Generator.NullableTypeExpression(Generator.IdentifierName("x")), "x?");
        VerifySyntax<TypeSyntax>(Generator.NullableTypeExpression(Generator.NullableTypeExpression(Generator.IdentifierName("x"))), "x?");

        var intType = _emptyCompilation.GetSpecialType(SpecialType.System_Int32);
        VerifySyntax<TupleElementSyntax>(Generator.TupleElementExpression(Generator.IdentifierName("x")), "x");
        VerifySyntax<TupleElementSyntax>(Generator.TupleElementExpression(Generator.IdentifierName("x"), "y"), "x y");
        VerifySyntax<TupleElementSyntax>(Generator.TupleElementExpression(intType), "global::System.Int32");
        VerifySyntax<TupleElementSyntax>(Generator.TupleElementExpression(intType, "y"), "global::System.Int32 y");
        VerifySyntax<TypeSyntax>(Generator.TupleTypeExpression(Generator.TupleElementExpression(Generator.IdentifierName("x")), Generator.TupleElementExpression(Generator.IdentifierName("y"))), "(x, y)");
        VerifySyntax<TypeSyntax>(Generator.TupleTypeExpression(new[] { intType, intType }), "(global::System.Int32, global::System.Int32)");
        VerifySyntax<TypeSyntax>(Generator.TupleTypeExpression(new[] { intType, intType }, ["x", "y"]), "(global::System.Int32 x, global::System.Int32 y)");
    }

    [Fact]
    public void TestSpecialTypeExpression()
    {
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Byte), "byte");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_SByte), "sbyte");

        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Int16), "short");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_UInt16), "ushort");

        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Int32), "int");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_UInt32), "uint");

        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Int64), "long");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_UInt64), "ulong");

        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Single), "float");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Double), "double");

        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Char), "char");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_String), "string");

        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Object), "object");
        VerifySyntax<TypeSyntax>(Generator.TypeExpression(SpecialType.System_Decimal), "decimal");
    }

    [Fact]
    public void TestSymbolTypeExpressions()
    {
        var genericType = _emptyCompilation.GetSpecialType(SpecialType.System_Collections_Generic_IEnumerable_T);
        VerifySyntax<QualifiedNameSyntax>(Generator.TypeExpression(genericType), "global::System.Collections.Generic.IEnumerable<T>");

        var arrayType = _emptyCompilation.CreateArrayTypeSymbol(_emptyCompilation.GetSpecialType(SpecialType.System_Int32));
        VerifySyntax<ArrayTypeSyntax>(Generator.TypeExpression(arrayType), "global::System.Int32[]");
    }

    [Fact]
    public void TestMathAndLogicExpressions()
    {
        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.NegateExpression(Generator.IdentifierName("x")), "-(x)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.AddExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) + (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.SubtractExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) - (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.MultiplyExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) * (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.DivideExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) / (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.ModuloExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) % (y)");

        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.BitwiseNotExpression(Generator.IdentifierName("x")), "~(x)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.BitwiseAndExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) & (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.BitwiseOrExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) | (y)");

        VerifySyntax<PrefixUnaryExpressionSyntax>(Generator.LogicalNotExpression(Generator.IdentifierName("x")), "!(x)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.LogicalAndExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) && (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.LogicalOrExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) || (y)");
    }

    [Fact]
    public void TestEqualityAndInequalityExpressions()
    {
        VerifySyntax<BinaryExpressionSyntax>(Generator.ReferenceEqualsExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) == (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.ValueEqualsExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) == (y)");

        VerifySyntax<BinaryExpressionSyntax>(Generator.ReferenceNotEqualsExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) != (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.ValueNotEqualsExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) != (y)");

        VerifySyntax<BinaryExpressionSyntax>(Generator.LessThanExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) < (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.LessThanOrEqualExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) <= (y)");

        VerifySyntax<BinaryExpressionSyntax>(Generator.GreaterThanExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) > (y)");
        VerifySyntax<BinaryExpressionSyntax>(Generator.GreaterThanOrEqualExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) >= (y)");
    }

    [Fact]
    public void TestConditionalExpressions()
    {
        VerifySyntax<BinaryExpressionSyntax>(Generator.CoalesceExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) ?? (y)");
        VerifySyntax<ConditionalExpressionSyntax>(Generator.ConditionalExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"), Generator.IdentifierName("z")), "(x) ? (y) : (z)");
    }

    [Fact]
    public void TestMemberAccessExpressions()
    {
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "x.y");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.IdentifierName("x"), "y"), "x.y");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.MemberAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")), "x.y.z");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")), "x(y).z");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.ElementAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")), "x[y].z");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.AddExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")), "((x) + (y)).z");
        VerifySyntax<MemberAccessExpressionSyntax>(Generator.MemberAccessExpression(Generator.NegateExpression(Generator.IdentifierName("x")), Generator.IdentifierName("y")), "(-(x)).y");
    }

    [Fact]
    public void TestArrayCreationExpressions()
    {
        VerifySyntax<ArrayCreationExpressionSyntax>(
            Generator.ArrayCreationExpression(Generator.IdentifierName("x"), Generator.LiteralExpression(10)),
            "new x[10]");

        VerifySyntax<ArrayCreationExpressionSyntax>(
            Generator.ArrayCreationExpression(Generator.IdentifierName("x"), new SyntaxNode[] { Generator.IdentifierName("y"), Generator.IdentifierName("z") }),
            """
            new x[]
            {
                y,
                z
            }
            """);
    }

    [Fact]
    public void TestObjectCreationExpressions()
    {
        VerifySyntax<ObjectCreationExpressionSyntax>(
            Generator.ObjectCreationExpression(Generator.IdentifierName("x")),
            "new x()");

        VerifySyntax<ObjectCreationExpressionSyntax>(
            Generator.ObjectCreationExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")),
            "new x(y)");

        var intType = _emptyCompilation.GetSpecialType(SpecialType.System_Int32);
        var listType = _emptyCompilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
        var listOfIntType = listType.Construct(intType);

        VerifySyntax<ObjectCreationExpressionSyntax>(
            Generator.ObjectCreationExpression(listOfIntType, Generator.IdentifierName("y")),
            "new global::System.Collections.Generic.List<global::System.Int32>(y)");  // should this be 'int' or if not shouldn't it have global::?
    }

    [Fact]
    public void TestElementAccessExpressions()
    {
        VerifySyntax<ElementAccessExpressionSyntax>(
            Generator.ElementAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")),
            "x[y]");

        VerifySyntax<ElementAccessExpressionSyntax>(
            Generator.ElementAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"), Generator.IdentifierName("z")),
            "x[y, z]");

        VerifySyntax<ElementAccessExpressionSyntax>(
            Generator.ElementAccessExpression(Generator.MemberAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")),
            "x.y[z]");

        VerifySyntax<ElementAccessExpressionSyntax>(
            Generator.ElementAccessExpression(Generator.ElementAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")),
            "x[y][z]");

        VerifySyntax<ElementAccessExpressionSyntax>(
            Generator.ElementAccessExpression(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")),
            "x(y)[z]");

        VerifySyntax<ElementAccessExpressionSyntax>(
            Generator.ElementAccessExpression(Generator.AddExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), Generator.IdentifierName("z")),
            "((x) + (y))[z]");
    }

    [Fact]
    public void TestCastAndConvertExpressions()
    {
        VerifySyntax<CastExpressionSyntax>(Generator.CastExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x)(y)");
        VerifySyntax<CastExpressionSyntax>(Generator.ConvertExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x)(y)");
    }

    [Fact]
    public void TestIsAndAsExpressions()
    {
        VerifySyntax<BinaryExpressionSyntax>(Generator.IsTypeExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) is y");
        VerifySyntax<BinaryExpressionSyntax>(Generator.TryCastExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "(x) as y");
        VerifySyntax<TypeOfExpressionSyntax>(Generator.TypeOfExpression(Generator.IdentifierName("x")), "typeof(x)");
    }

    [Fact]
    public void TestInvocationExpressions()
    {
        // without explicit arguments
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.IdentifierName("x")), "x()");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "x(y)");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"), Generator.IdentifierName("z")), "x(y, z)");

        // using explicit arguments
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.Argument(Generator.IdentifierName("y"))), "x(y)");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.Argument(RefKind.Ref, Generator.IdentifierName("y"))), "x(ref y)");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.Argument(RefKind.Out, Generator.IdentifierName("y"))), "x(out y)");

        // auto parenthesizing
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.MemberAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"))), "x.y()");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.ElementAccessExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"))), "x[y]()");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.InvocationExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"))), "x(y)()");
        VerifySyntax<InvocationExpressionSyntax>(Generator.InvocationExpression(Generator.AddExpression(Generator.IdentifierName("x"), Generator.IdentifierName("y"))), "((x) + (y))()");
    }

    [Fact]
    public void TestAssignmentStatement()
        => VerifySyntax<AssignmentExpressionSyntax>(Generator.AssignmentStatement(Generator.IdentifierName("x"), Generator.IdentifierName("y")), "x = (y)");

    [Fact]
    public void TestExpressionStatement()
    {
        VerifySyntax<ExpressionStatementSyntax>(Generator.ExpressionStatement(Generator.IdentifierName("x")), "x;");
        VerifySyntax<ExpressionStatementSyntax>(Generator.ExpressionStatement(Generator.InvocationExpression(Generator.IdentifierName("x"))), "x();");
    }

    [Fact]
    public void TestLocalDeclarationStatements()
    {
        VerifySyntax<LocalDeclarationStatementSyntax>(Generator.LocalDeclarationStatement(Generator.IdentifierName("x"), "y"), "x y;");
        VerifySyntax<LocalDeclarationStatementSyntax>(Generator.LocalDeclarationStatement(Generator.IdentifierName("x"), "y", Generator.IdentifierName("z")), "x y = z;");

        VerifySyntax<LocalDeclarationStatementSyntax>(Generator.LocalDeclarationStatement(Generator.IdentifierName("x"), "y", isConst: true), "const x y;");
        VerifySyntax<LocalDeclarationStatementSyntax>(Generator.LocalDeclarationStatement(Generator.IdentifierName("x"), "y", Generator.IdentifierName("z"), isConst: true), "const x y = z;");

        VerifySyntax<LocalDeclarationStatementSyntax>(Generator.LocalDeclarationStatement("y", Generator.IdentifierName("z")), "var y = z;");
    }

    [Fact]
    public void TestAddHandlerExpressions()
    {
        VerifySyntax<AssignmentExpressionSyntax>(
            Generator.AddEventHandler(Generator.IdentifierName("@event"), Generator.IdentifierName("handler")),
            "@event += (handler)");
    }

    [Fact]
    public void TestSubtractHandlerExpressions()
    {
        VerifySyntax<AssignmentExpressionSyntax>(
            Generator.RemoveEventHandler(Generator.IdentifierName("@event"),
            Generator.IdentifierName("handler")), "@event -= (handler)");
    }

    [Fact]
    public void TestAwaitExpressions()
        => VerifySyntax<AwaitExpressionSyntax>(Generator.AwaitExpression(Generator.IdentifierName("x")), "await x");

    [Fact]
    public void TestNameOfExpressions()
        => VerifySyntax<InvocationExpressionSyntax>(Generator.NameOfExpression(Generator.IdentifierName("x")), "nameof(x)");

    [Fact]
    public void TestTupleExpression()
    {
        VerifySyntax<TupleExpressionSyntax>(Generator.TupleExpression(
            [Generator.IdentifierName("x"), Generator.IdentifierName("y")]), "(x, y)");

        VerifySyntax<TupleExpressionSyntax>(Generator.TupleExpression([
            Generator.Argument("goo", RefKind.None, Generator.IdentifierName("x")),
            Generator.Argument("bar", RefKind.None, Generator.IdentifierName("y"))]), "(goo: x, bar: y)");
    }

    [Fact]
    public void TestReturnStatements()
    {
        VerifySyntax<ReturnStatementSyntax>(Generator.ReturnStatement(), "return;");
        VerifySyntax<ReturnStatementSyntax>(Generator.ReturnStatement(Generator.IdentifierName("x")), "return x;");
    }

    [Fact]
    public void TestYieldReturnStatements()
    {
        VerifySyntax<YieldStatementSyntax>(Generator.YieldReturnStatement(Generator.LiteralExpression(1)), "yield return 1;");
        VerifySyntax<YieldStatementSyntax>(Generator.YieldReturnStatement(Generator.IdentifierName("x")), "yield return x;");
    }

    [Fact]
    public void TestThrowStatements()
    {
        VerifySyntax<ThrowStatementSyntax>(Generator.ThrowStatement(), "throw;");
        VerifySyntax<ThrowStatementSyntax>(Generator.ThrowStatement(Generator.IdentifierName("x")), "throw x;");
    }

    [Fact]
    public void TestIfStatements()
    {
        VerifySyntax<IfStatementSyntax>(
            Generator.IfStatement(Generator.IdentifierName("x"), new SyntaxNode[] { }),
            "if (x)\r\n{\r\n}");

        VerifySyntax<IfStatementSyntax>(
            Generator.IfStatement(Generator.IdentifierName("x"), new SyntaxNode[] { }, new SyntaxNode[] { }),
            "if (x)\r\n{\r\n}\r\nelse\r\n{\r\n}");

        VerifySyntax<IfStatementSyntax>(
            Generator.IfStatement(Generator.IdentifierName("x"),
                new SyntaxNode[] { Generator.IdentifierName("y") }),
            "if (x)\r\n{\r\n    y;\r\n}");

        VerifySyntax<IfStatementSyntax>(
            Generator.IfStatement(Generator.IdentifierName("x"),
                new SyntaxNode[] { Generator.IdentifierName("y") },
                new SyntaxNode[] { Generator.IdentifierName("z") }),
            "if (x)\r\n{\r\n    y;\r\n}\r\nelse\r\n{\r\n    z;\r\n}");

        VerifySyntax<IfStatementSyntax>(
            Generator.IfStatement(Generator.IdentifierName("x"),
                new SyntaxNode[] { Generator.IdentifierName("y") },
                Generator.IfStatement(Generator.IdentifierName("p"), new SyntaxNode[] { Generator.IdentifierName("q") })),
            "if (x)\r\n{\r\n    y;\r\n}\r\nelse if (p)\r\n{\r\n    q;\r\n}");

        VerifySyntax<IfStatementSyntax>(
            Generator.IfStatement(Generator.IdentifierName("x"),
                new SyntaxNode[] { Generator.IdentifierName("y") },
                Generator.IfStatement(Generator.IdentifierName("p"), new SyntaxNode[] { Generator.IdentifierName("q") }, Generator.IdentifierName("z"))),
            "if (x)\r\n{\r\n    y;\r\n}\r\nelse if (p)\r\n{\r\n    q;\r\n}\r\nelse\r\n{\r\n    z;\r\n}");
    }

    [Fact]
    public void TestSwitchStatements()
    {
        VerifySyntax<SwitchStatementSyntax>(
            Generator.SwitchStatement(Generator.IdentifierName("x"),
                Generator.SwitchSection(Generator.IdentifierName("y"),
                    [Generator.IdentifierName("z")])),
            "switch (x)\r\n{\r\n    case y:\r\n        z;\r\n}");

        VerifySyntax<SwitchStatementSyntax>(
            Generator.SwitchStatement(Generator.IdentifierName("x"),
                Generator.SwitchSection(
                    [Generator.IdentifierName("y"), Generator.IdentifierName("p"), Generator.IdentifierName("q")],
                    [Generator.IdentifierName("z")])),
            "switch (x)\r\n{\r\n    case y:\r\n    case p:\r\n    case q:\r\n        z;\r\n}");

        VerifySyntax<SwitchStatementSyntax>(
            Generator.SwitchStatement(Generator.IdentifierName("x"),
                Generator.SwitchSection(Generator.IdentifierName("y"),
                    [Generator.IdentifierName("z")]),
                Generator.SwitchSection(Generator.IdentifierName("a"),
                    [Generator.IdentifierName("b")])),
            "switch (x)\r\n{\r\n    case y:\r\n        z;\r\n    case a:\r\n        b;\r\n}");

        VerifySyntax<SwitchStatementSyntax>(
            Generator.SwitchStatement(Generator.IdentifierName("x"),
                Generator.SwitchSection(Generator.IdentifierName("y"),
                    [Generator.IdentifierName("z")]),
                Generator.DefaultSwitchSection(
                    [Generator.IdentifierName("b")])),
            "switch (x)\r\n{\r\n    case y:\r\n        z;\r\n    default:\r\n        b;\r\n}");

        VerifySyntax<SwitchStatementSyntax>(
            Generator.SwitchStatement(Generator.IdentifierName("x"),
                Generator.SwitchSection(Generator.IdentifierName("y"),
                    [Generator.ExitSwitchStatement()])),
            "switch (x)\r\n{\r\n    case y:\r\n        break;\r\n}");

        VerifySyntax<SwitchStatementSyntax>(
            Generator.SwitchStatement(Generator.TupleExpression([Generator.IdentifierName("x1"), Generator.IdentifierName("x2")]),
                Generator.SwitchSection(Generator.IdentifierName("y"),
                    [Generator.IdentifierName("z")])),
            "switch (x1, x2)\r\n{\r\n    case y:\r\n        z;\r\n}");

    }

    [Fact]
    public void TestUsingStatements()
    {
        VerifySyntax<UsingStatementSyntax>(
            Generator.UsingStatement(Generator.IdentifierName("x"), [Generator.IdentifierName("y")]),
            "using (x)\r\n{\r\n    y;\r\n}");

        VerifySyntax<UsingStatementSyntax>(
            Generator.UsingStatement("x", Generator.IdentifierName("y"), [Generator.IdentifierName("z")]),
            "using (var x = y)\r\n{\r\n    z;\r\n}");

        VerifySyntax<UsingStatementSyntax>(
            Generator.UsingStatement(Generator.IdentifierName("x"), "y", Generator.IdentifierName("z"), [Generator.IdentifierName("q")]),
            "using (x y = z)\r\n{\r\n    q;\r\n}");
    }

    [Fact]
    public void TestLockStatements()
    {
        VerifySyntax<LockStatementSyntax>(
            Generator.LockStatement(Generator.IdentifierName("x"), [Generator.IdentifierName("y")]),
            "lock (x)\r\n{\r\n    y;\r\n}");
    }

    [Fact]
    public void TestTryCatchStatements()
    {
        VerifySyntax<TryStatementSyntax>(
            Generator.TryCatchStatement(
                [Generator.IdentifierName("x")],
                Generator.CatchClause(Generator.IdentifierName("y"), "z",
                    [Generator.IdentifierName("a")])),
            "try\r\n{\r\n    x;\r\n}\r\ncatch (y z)\r\n{\r\n    a;\r\n}");

        VerifySyntax<TryStatementSyntax>(
            Generator.TryCatchStatement(
                [Generator.IdentifierName("s")],
                Generator.CatchClause(Generator.IdentifierName("x"), "y",
                    [Generator.IdentifierName("z")]),
                Generator.CatchClause(Generator.IdentifierName("a"), "b",
                    [Generator.IdentifierName("c")])),
            "try\r\n{\r\n    s;\r\n}\r\ncatch (x y)\r\n{\r\n    z;\r\n}\r\ncatch (a b)\r\n{\r\n    c;\r\n}");

        VerifySyntax<TryStatementSyntax>(
            Generator.TryCatchStatement(
                [Generator.IdentifierName("s")],
                [Generator.CatchClause(Generator.IdentifierName("x"), "y", [Generator.IdentifierName("z")])],
                [Generator.IdentifierName("a")]),
            "try\r\n{\r\n    s;\r\n}\r\ncatch (x y)\r\n{\r\n    z;\r\n}\r\nfinally\r\n{\r\n    a;\r\n}");

        VerifySyntax<TryStatementSyntax>(
            Generator.TryFinallyStatement(
                [Generator.IdentifierName("x")],
                [Generator.IdentifierName("a")]),
            "try\r\n{\r\n    x;\r\n}\r\nfinally\r\n{\r\n    a;\r\n}");
    }

    [Fact]
    public void TestWhileStatements()
    {
        VerifySyntax<WhileStatementSyntax>(
            Generator.WhileStatement(Generator.IdentifierName("x"),
                [Generator.IdentifierName("y")]),
            "while (x)\r\n{\r\n    y;\r\n}");

        VerifySyntax<WhileStatementSyntax>(
            Generator.WhileStatement(Generator.IdentifierName("x"), null),
            "while (x)\r\n{\r\n}");
    }

    [Fact]
    public void TestLambdaExpressions()
    {
        VerifySyntax<SimpleLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression("x", Generator.IdentifierName("y")),
            "x => y");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression(new[] { Generator.LambdaParameter("x"), Generator.LambdaParameter("y") }, Generator.IdentifierName("z")),
            "(x, y) => z");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression(new SyntaxNode[] { }, Generator.IdentifierName("y")),
            "() => y");

        VerifySyntax<SimpleLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression("x", Generator.IdentifierName("y")),
            "x => y");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression(new[] { Generator.LambdaParameter("x"), Generator.LambdaParameter("y") }, Generator.IdentifierName("z")),
            "(x, y) => z");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression(new SyntaxNode[] { }, Generator.IdentifierName("y")),
            "() => y");

        VerifySyntax<SimpleLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression("x", [Generator.ReturnStatement(Generator.IdentifierName("y"))]),
            "x =>\r\n{\r\n    return y;\r\n}");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression(new[] { Generator.LambdaParameter("x"), Generator.LambdaParameter("y") }, [Generator.ReturnStatement(Generator.IdentifierName("z"))]),
            "(x, y) =>\r\n{\r\n    return z;\r\n}");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression(new SyntaxNode[] { }, [Generator.ReturnStatement(Generator.IdentifierName("y"))]),
            "() =>\r\n{\r\n    return y;\r\n}");

        VerifySyntax<SimpleLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression("x", [Generator.IdentifierName("y")]),
            "x =>\r\n{\r\n    y;\r\n}");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression(new[] { Generator.LambdaParameter("x"), Generator.LambdaParameter("y") }, [Generator.IdentifierName("z")]),
            "(x, y) =>\r\n{\r\n    z;\r\n}");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression(new SyntaxNode[] { }, [Generator.IdentifierName("y")]),
            "() =>\r\n{\r\n    y;\r\n}");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression(new[] { Generator.LambdaParameter("x", Generator.IdentifierName("y")) }, Generator.IdentifierName("z")),
            "(y x) => z");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.ValueReturningLambdaExpression(new[] { Generator.LambdaParameter("x", Generator.IdentifierName("y")), Generator.LambdaParameter("a", Generator.IdentifierName("b")) }, Generator.IdentifierName("z")),
            "(y x, b a) => z");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression(new[] { Generator.LambdaParameter("x", Generator.IdentifierName("y")) }, Generator.IdentifierName("z")),
            "(y x) => z");

        VerifySyntax<ParenthesizedLambdaExpressionSyntax>(
            Generator.VoidReturningLambdaExpression(new[] { Generator.LambdaParameter("x", Generator.IdentifierName("y")), Generator.LambdaParameter("a", Generator.IdentifierName("b")) }, Generator.IdentifierName("z")),
            "(y x, b a) => z");
    }
    #endregion

    #region Declarations
    [Fact]
    public void TestFieldDeclarations()
    {
        VerifySyntax<FieldDeclarationSyntax>(
            Generator.FieldDeclaration("fld", Generator.TypeExpression(SpecialType.System_Int32)),
            "int fld;");

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.FieldDeclaration("fld", Generator.TypeExpression(SpecialType.System_Int32), initializer: Generator.LiteralExpression(0)),
            "int fld = 0;");

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.FieldDeclaration("fld", Generator.TypeExpression(SpecialType.System_Int32), accessibility: Accessibility.Public),
            "public int fld;");

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.FieldDeclaration("fld", Generator.TypeExpression(SpecialType.System_Int32), accessibility: Accessibility.NotApplicable, modifiers: DeclarationModifiers.Static | DeclarationModifiers.ReadOnly),
            "static readonly int fld;");

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.FieldDeclaration("fld", Generator.TypeExpression(SpecialType.System_Int32), accessibility: Accessibility.NotApplicable, modifiers: DeclarationModifiers.Required),
            "required int fld;");
    }

    [Fact]
    public void TestMethodDeclarations()
    {
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m"),
            "void m()\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", typeParameters: ["x", "y"]),
            "void m<x, y>()\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("x")),
            "x m()\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("x"), statements: [Generator.IdentifierName("y")]),
            "x m()\r\n{\r\n    y;\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", parameters: [Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], returnType: Generator.IdentifierName("x")),
            "x m(y z)\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", parameters: [Generator.ParameterDeclaration("z", Generator.IdentifierName("y"), Generator.IdentifierName("a"))], returnType: Generator.IdentifierName("x")),
            "x m(y z = a)\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("x"), accessibility: Accessibility.Public),
            "public x m()\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("x"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Abstract),
            "public abstract x m();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Partial),
            "partial void m();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Partial, statements: [Generator.IdentifierName("y")]),
            "partial void m()\r\n{\r\n    y;\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Partial | DeclarationModifiers.Async, statements: null),
            "partial void m();");
    }

    [Fact]
    public void TestOperatorDeclaration()
    {
        var parameterTypes = new[]
        {
            _emptyCompilation.GetSpecialType(SpecialType.System_Int32),
            _emptyCompilation.GetSpecialType(SpecialType.System_String)
        };
        var parameters = parameterTypes.Select((t, i) => Generator.ParameterDeclaration("p" + i, Generator.TypeExpression(t))).ToList();
        var returnType = Generator.TypeExpression(SpecialType.System_Boolean);

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Addition, parameters, returnType),
            "bool operator +(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.BitwiseAnd, parameters, returnType),
            "bool operator &(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.BitwiseOr, parameters, returnType),
            "bool operator |(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Decrement, parameters, returnType),
            "bool operator --(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Division, parameters, returnType),
            "bool operator /(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Equality, parameters, returnType),
            "bool operator ==(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.ExclusiveOr, parameters, returnType),
            "bool operator ^(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.False, parameters, returnType),
            "bool operator false (global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.GreaterThan, parameters, returnType),
            "bool operator>(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.GreaterThanOrEqual, parameters, returnType),
            "bool operator >=(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Increment, parameters, returnType),
            "bool operator ++(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Inequality, parameters, returnType),
            "bool operator !=(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.LeftShift, parameters, returnType),
            "bool operator <<(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.LessThan, parameters, returnType),
            "bool operator <(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.LessThanOrEqual, parameters, returnType),
            "bool operator <=(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.LogicalNot, parameters, returnType),
            "bool operator !(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Modulus, parameters, returnType),
            "bool operator %(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Multiply, parameters, returnType),
            "bool operator *(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.OnesComplement, parameters, returnType),
            "bool operator ~(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.RightShift, parameters, returnType),
            "bool operator >>(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.UnsignedRightShift, parameters, returnType),
            "bool operator >>>(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Subtraction, parameters, returnType),
            "bool operator -(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.True, parameters, returnType),
            "bool operator true (global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.UnaryNegation, parameters, returnType),
            "bool operator -(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.UnaryPlus, parameters, returnType),
            "bool operator +(global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        // Conversion operators

        VerifySyntax<ConversionOperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.ImplicitConversion, parameters, returnType),
            "implicit operator bool (global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");

        VerifySyntax<ConversionOperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.ExplicitConversion, parameters, returnType),
            "explicit operator bool (global::System.Int32 p0, global::System.String p1)\r\n{\r\n}");
    }

    [Fact]
    public void TestInstanceOperatorsDeclaration()
    {
        var returnType = Generator.TypeExpression(SpecialType.System_Void);

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Increment, [], returnType),
            "void operator ++()\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.Decrement, [], returnType),
            "void operator --()\r\n{\r\n}");

        var parameterTypes = new[]
        {
            _emptyCompilation.GetSpecialType(SpecialType.System_Int32)
        };
        var parameters = parameterTypes.Select((t, i) => Generator.ParameterDeclaration("p" + i, Generator.TypeExpression(t))).ToList();

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.AdditionAssignment, parameters, returnType),
            "void operator +=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.SubtractionAssignment, parameters, returnType),
            "void operator -=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.MultiplicationAssignment, parameters, returnType),
            "void operator *=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.DivisionAssignment, parameters, returnType),
            "void operator /=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.ModulusAssignment, parameters, returnType),
            "void operator %=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.ExclusiveOrAssignment, parameters, returnType),
            "void operator ^=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.BitwiseAndAssignment, parameters, returnType),
            "void operator &=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.BitwiseOrAssignment, parameters, returnType),
            "void operator |=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.LeftShiftAssignment, parameters, returnType),
            "void operator <<=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.RightShiftAssignment, parameters, returnType),
            "void operator >>=(global::System.Int32 p0)\r\n{\r\n}");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(OperatorKind.UnsignedRightShiftAssignment, parameters, returnType),
            "void operator >>>=(global::System.Int32 p0)\r\n{\r\n}");
    }

    [Fact, WorkItem(63410, "https://github.com/dotnet/roslyn/issues/63410")]
    public void TestCheckedOperator()
    {
        var comp = CSharpCompilation.Create(null, [SyntaxFactory.ParseSyntaxTree("""
            public class C
            {
                public static C operator checked ++(C x) => x;
                public static C operator ++(C x) => x;
            }
            """)]);
        var operatorSymbol = (IMethodSymbol)comp.GetTypeByMetadataName("C").GetMembers(WellKnownMemberNames.CheckedIncrementOperatorName).Single();
        VerifySyntax<OperatorDeclarationSyntax>(Generator.OperatorDeclaration(operatorSymbol), "public static global::C operator checked ++(global::C x)\r\n{\r\n}");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65833")]
    public void TestConversionOperatorDeclaration()
    {
        var gcHandleType = _emptyCompilation.GetTypeByMetadataName(typeof(GCHandle).FullName);
        var conversion = gcHandleType.GetMembers().OfType<IMethodSymbol>().Single(m =>
            m.Name == WellKnownMemberNames.ExplicitConversionName && m.Parameters[0].Type.Equals(gcHandleType));

        VerifySyntax<ConversionOperatorDeclarationSyntax>(
            Generator.Declaration(conversion),
            "public static explicit operator global::System.IntPtr(global::System.Runtime.InteropServices.GCHandle value)\r\n{\r\n}");

        var doubleType = _emptyCompilation.GetSpecialType(SpecialType.System_Decimal);
        conversion = doubleType.GetMembers().OfType<IMethodSymbol>().Single(m =>
            m.Name == WellKnownMemberNames.ImplicitConversionName && m.Parameters[0].Type.Equals(_emptyCompilation.GetSpecialType(SpecialType.System_Byte)));
        VerifySyntax<ConversionOperatorDeclarationSyntax>(
            Generator.Declaration(conversion),
            "public static implicit operator global::System.Decimal(global::System.Byte value)\r\n{\r\n}");
    }

    [Fact, WorkItem(77101, "https://github.com/dotnet/roslyn/issues/77101")]
    public void TestExplicitCheckedOperatorFromSymbol()
    {
        var compilation = CSharpCompilation.Create("Test", [SyntaxFactory.ParseSyntaxTree("""
            public class C
            {
                public static explicit operator checked int(C c) => 0;
            }
            """)]);

        var c = compilation.GetTypeByMetadataName("C");
        var op = c.GetMembers().OfType<IMethodSymbol>().Where(m => m.MethodKind == MethodKind.Conversion).Single();

        VerifySyntax<ConversionOperatorDeclarationSyntax>(
            Generator.OperatorDeclaration(op),
            "public static explicit operator checked global::System.Int32(global::C c)\r\n{\r\n}");
    }

    [Fact]
    public void TestConstructorDeclaration()
    {
        VerifySyntax<ConstructorDeclarationSyntax>(
            Generator.ConstructorDeclaration(),
            "ctor()\r\n{\r\n}");

        VerifySyntax<ConstructorDeclarationSyntax>(
            Generator.ConstructorDeclaration("c"),
            "c()\r\n{\r\n}");

        VerifySyntax<ConstructorDeclarationSyntax>(
            Generator.ConstructorDeclaration("c", accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Static),
            "public static c()\r\n{\r\n}");

        VerifySyntax<ConstructorDeclarationSyntax>(
            Generator.ConstructorDeclaration("c", [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))]),
            "c(t p)\r\n{\r\n}");

        VerifySyntax<ConstructorDeclarationSyntax>(
            Generator.ConstructorDeclaration("c",
                parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))],
                baseConstructorArguments: [Generator.IdentifierName("p")]),
            "c(t p) : base(p)\r\n{\r\n}");
    }

    [Fact]
    public void TestPropertyDeclarations()
    {
        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract | DeclarationModifiers.ReadOnly),
            "abstract x p { get; }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract | DeclarationModifiers.WriteOnly),
            "abstract x p { set; }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.ReadOnly),
            "x p { get; }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.ReadOnly, getAccessorStatements: []),
            "x p\r\n{\r\n    get\r\n    {\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.WriteOnly),
            "x p { set; }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.WriteOnly, setAccessorStatements: []),
            "x p\r\n{\r\n    set\r\n    {\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract),
            "abstract x p { get; set; }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Required),
            "required x p { get; set; }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.ReadOnly, getAccessorStatements: [Generator.IdentifierName("y")]),
            "x p\r\n{\r\n    get\r\n    {\r\n        y;\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), modifiers: DeclarationModifiers.WriteOnly, setAccessorStatements: [Generator.IdentifierName("y")]),
            "x p\r\n{\r\n    set\r\n    {\r\n        y;\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), setAccessorStatements: [Generator.IdentifierName("y")]),
            "x p\r\n{\r\n    get;\r\n    set\r\n    {\r\n        y;\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), getAccessorStatements: [], setAccessorStatements: [Generator.IdentifierName("y")]),
            "x p\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n        y;\r\n    }\r\n}");
    }

    [Fact]
    public void TestIndexerDeclarations()
    {
        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract | DeclarationModifiers.ReadOnly),
            "abstract x this[y z] { get; }");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract | DeclarationModifiers.WriteOnly),
            "abstract x this[y z] { set; }");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract),
            "abstract x this[y z] { get; set; }");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.ReadOnly),
            "x this[y z]\r\n{\r\n    get\r\n    {\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.WriteOnly),
            "x this[y z]\r\n{\r\n    set\r\n    {\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.ReadOnly,
                getAccessorStatements: [Generator.IdentifierName("a")]),
            "x this[y z]\r\n{\r\n    get\r\n    {\r\n        a;\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.WriteOnly,
                setAccessorStatements: [Generator.IdentifierName("a")]),
            "x this[y z]\r\n{\r\n    set\r\n    {\r\n        a;\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x")),
            "x this[y z]\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"),
                setAccessorStatements: [Generator.IdentifierName("a")]),
            "x this[y z]\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n        a;\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"),
                getAccessorStatements: [Generator.IdentifierName("a")], setAccessorStatements: [Generator.IdentifierName("b")]),
            "x this[y z]\r\n{\r\n    get\r\n    {\r\n        a;\r\n    }\r\n\r\n    set\r\n    {\r\n        b;\r\n    }\r\n}");
    }

    [Fact]
    public void TestEventFieldDeclarations()
    {
        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.EventDeclaration("ef", Generator.IdentifierName("t")),
            "event t ef;");

        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.EventDeclaration("ef", Generator.IdentifierName("t"), accessibility: Accessibility.Public),
            "public event t ef;");

        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.EventDeclaration("ef", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Static),
            "static event t ef;");
    }

    [Fact]
    public void TestEventPropertyDeclarations()
    {
        VerifySyntax<EventDeclarationSyntax>(
            Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract),
            "abstract event t ep { add; remove; }");

        VerifySyntax<EventDeclarationSyntax>(
            Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Abstract),
            "public abstract event t ep { add; remove; }");

        VerifySyntax<EventDeclarationSyntax>(
            Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t")),
            "event t ep\r\n{\r\n    add\r\n    {\r\n    }\r\n\r\n    remove\r\n    {\r\n    }\r\n}");

        VerifySyntax<EventDeclarationSyntax>(
            Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), addAccessorStatements: [Generator.IdentifierName("s")], removeAccessorStatements: [Generator.IdentifierName("s2")]),
            "event t ep\r\n{\r\n    add\r\n    {\r\n        s;\r\n    }\r\n\r\n    remove\r\n    {\r\n        s2;\r\n    }\r\n}");
    }

    [Fact]
    public void TestAsPublicInterfaceImplementation()
    {
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
                Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "public t m()\r\n{\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), accessibility: Accessibility.Private, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "public t p\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
                Generator.IndexerDeclaration(parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("a"))], type: Generator.IdentifierName("t"), accessibility: Accessibility.Internal, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "public t this[a p]\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n    }\r\n}");

        // convert private to public
        var pim = Generator.AsPrivateInterfaceImplementation(
                Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), accessibility: Accessibility.Private, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i"));

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(pim, Generator.IdentifierName("i2")),
            "public t m()\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(pim, Generator.IdentifierName("i2"), "m2"),
            "public t m2()\r\n{\r\n}");

        #region ExpressionBodyTests
        VerifySyntax<MethodDeclarationSyntax>(
               Generator.AsPublicInterfaceImplementation(
               MethodDeclaration(
                   PredefinedType(
                       Token(
                           [],
                           SyntaxKind.ObjectKeyword,
                           TriviaList(
                               Space))),
                   Identifier("DoSomething"))
               .WithExplicitInterfaceSpecifier(
                    ExplicitInterfaceSpecifier(
                       IdentifierName("IGeneral")))
               .WithParameterList(
                   ParameterList()
                    .WithCloseParenToken(
                       Token(
                           [],
                           SyntaxKind.CloseParenToken,
                           TriviaList(
                               Space))))
               .WithExpressionBody(
                   ArrowExpressionClause(
                       ImplicitObjectCreationExpression())
                   .WithArrowToken(
                       Token(
                           [],
                           SyntaxKind.EqualsGreaterThanToken,
                           TriviaList(
                               Space))))
               .WithSemicolonToken(
                   SemicolonToken),
               Generator.IdentifierName("i")),
               "public object DoSomething() => new();");

        VerifySyntax<OperatorDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            OperatorDeclaration(
                PredefinedType(
                    IntKeyword),
                PlusToken)
            .WithModifiers([
                PublicKeyword,
                StaticKeyword])
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    GenericName(
                        Identifier("IGeneral"))
                    .WithTypeArgumentList(
                        TypeArgumentList([IdentifierName("C")]))))
            .WithParameterList(
                ParameterList(
                    SeparatedList<ParameterSyntax>(
                        new SyntaxNodeOrToken[]{
                            Parameter(
                                Identifier("x"))
                            .WithType(
                                IdentifierName("C")),
                            CommaToken,
                            Parameter(
                                Identifier("y"))
                            .WithType(
                                IdentifierName("C"))})))
            .WithExpressionBody(
                ArrowExpressionClause(
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(0))))
            .WithSemicolonToken(
                SemicolonToken)
            .NormalizeWhitespace(),
            Generator.IdentifierName("i")),
            "public static int operator +(C x, C y) => 0;");

        VerifySyntax<ConversionOperatorDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            ConversionOperatorDeclaration(
                Token(
                    [],
                    SyntaxKind.ImplicitKeyword,
                    TriviaList(
                        Space)),
                PredefinedType(
                    StringKeyword))
            .WithModifiers(
                [Token(
                    [],
                    SyntaxKind.StaticKeyword,
                    TriviaList(
                        Space))])
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    GenericName(
                        Identifier("IGeneral"))
                    .WithTypeArgumentList(
                        TypeArgumentList([IdentifierName("C")]))))
            .WithOperatorKeyword(
                Token(
                    [],
                    SyntaxKind.OperatorKeyword,
                    TriviaList(
                        Space)))
            .WithParameterList(
                ParameterList(
                    [Parameter(
                        Identifier("x"))
                        .WithType(
                            IdentifierName(
                                Identifier(
                                    [],
                                    "C",
                                    TriviaList(
                                        Space))))])
                .WithCloseParenToken(
                    Token(
                        [],
                        SyntaxKind.CloseParenToken,
                        TriviaList(
                            Space))))
            .WithExpressionBody(
                ArrowExpressionClause(
                    LiteralExpression(
                        SyntaxKind.NullLiteralExpression))
                .WithArrowToken(
                    Token(
                        [],
                        SyntaxKind.EqualsGreaterThanToken,
                        TriviaList(
                            Space))))
            .WithSemicolonToken(
                SemicolonToken)
            .NormalizeWhitespace(),
                Generator.IdentifierName("i")),
                "public static implicit operator string (C x) => null;");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            PropertyDeclaration(
                PredefinedType(
                    IntKeyword),
                Identifier("Num"))
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    IdentifierName("IGeneral")))
            .WithExpressionBody(
                ArrowExpressionClause(
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(0))))
            .WithSemicolonToken(
                SemicolonToken)
            .NormalizeWhitespace(),
            Generator.IdentifierName("i")),
            "public int Num => 0;");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            PropertyDeclaration(
                PredefinedType(
                    IntKeyword),
                Identifier("Num"))
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    IdentifierName("IGeneral")))
            .WithAccessorList(
                AccessorList([
                        AccessorDeclaration(
                            SyntaxKind.GetAccessorDeclaration)
                        .WithExpressionBody(
                            ArrowExpressionClause(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(0))))
                        .WithSemicolonToken(
                            SemicolonToken)]))
            .NormalizeWhitespace(),
            Generator.IdentifierName("i")),
            "public int Num { get => 0; }");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            IndexerDeclaration(
                PredefinedType(
                    IntKeyword))
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    IdentifierName("IGeneral")))
            .WithParameterList(
                BracketedParameterList(
                    [Parameter(
                        Identifier("index"))
                        .WithType(
                            PredefinedType(
                                IntKeyword))]))
            .WithExpressionBody(
                ArrowExpressionClause(
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(0))))
            .WithSemicolonToken(
                SemicolonToken)
            .NormalizeWhitespace(),
            Generator.IdentifierName("i")),
            "public int this[int index] => 0;");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            IndexerDeclaration(
                PredefinedType(
                    IntKeyword))
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    IdentifierName("IGeneral")))
            .WithParameterList(
                BracketedParameterList(
                    [Parameter(
                        Identifier("index"))
                        .WithType(
                            PredefinedType(
                                IntKeyword))]))
            .WithAccessorList(
                AccessorList(
                    SingletonList<AccessorDeclarationSyntax>(
                        AccessorDeclaration(
                            SyntaxKind.GetAccessorDeclaration)
                        .WithExpressionBody(
                            ArrowExpressionClause(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(0))))
                        .WithSemicolonToken(
                            SemicolonToken))))
            .NormalizeWhitespace(),
            Generator.IdentifierName("i")),
            "public int this[int index] { get => 0; }");

        VerifySyntax<EventDeclarationSyntax>(
            Generator.AsPublicInterfaceImplementation(
            EventDeclaration(
                IdentifierName("EventHandler"),
                Identifier("Event"))
            .WithExplicitInterfaceSpecifier(
                ExplicitInterfaceSpecifier(
                    IdentifierName("IGeneral")))
            .WithAccessorList(
                AccessorList([
                    AccessorDeclaration(
                        SyntaxKind.AddAccessorDeclaration)
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            LiteralExpression(
                                SyntaxKind.NullLiteralExpression)))
                    .WithSemicolonToken(
                        SemicolonToken),
                    AccessorDeclaration(
                        SyntaxKind.RemoveAccessorDeclaration)
                    .WithExpressionBody(
                        ArrowExpressionClause(
                            LiteralExpression(
                                SyntaxKind.NullLiteralExpression)))
                    .WithSemicolonToken(
                        SemicolonToken)]))
            .NormalizeWhitespace(),
            Generator.IdentifierName("i")),
            "public event EventHandler Event { add => null; remove => null; }");
        #endregion
    }

    [Fact]
    public void TestAsPrivateInterfaceImplementation()
    {
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AsPrivateInterfaceImplementation(
                Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), accessibility: Accessibility.Private, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "t i.m()\r\n{\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.AsPrivateInterfaceImplementation(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), accessibility: Accessibility.Internal, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "t i.p\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.AsPrivateInterfaceImplementation(
                Generator.IndexerDeclaration(parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("a"))], type: Generator.IdentifierName("t"), accessibility: Accessibility.Protected, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "t i.this[a p]\r\n{\r\n    get\r\n    {\r\n    }\r\n\r\n    set\r\n    {\r\n    }\r\n}");

        VerifySyntax<EventDeclarationSyntax>(
            Generator.AsPrivateInterfaceImplementation(
                Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i")),
            "event t i.ep\r\n{\r\n    add\r\n    {\r\n    }\r\n\r\n    remove\r\n    {\r\n    }\r\n}");

        // convert public to private
        var pim = Generator.AsPublicInterfaceImplementation(
                Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), accessibility: Accessibility.Private, modifiers: DeclarationModifiers.Abstract),
                Generator.IdentifierName("i"));

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AsPrivateInterfaceImplementation(pim, Generator.IdentifierName("i2")),
            "t i2.m()\r\n{\r\n}");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AsPrivateInterfaceImplementation(pim, Generator.IdentifierName("i2"), "m2"),
            "t i2.m2()\r\n{\r\n}");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/3928")]
    public void TestAsPrivateInterfaceImplementationRemovesConstraints()
    {
        var code = """
            public interface IFace
            {
                void Method<T>() where T : class;
            }
            """;

        var cu = ParseCompilationUnit(code);
        var iface = cu.Members[0];
        var method = Generator.GetMembers(iface)[0];

        var privateMethod = Generator.AsPrivateInterfaceImplementation(method, Generator.IdentifierName("IFace"));

        VerifySyntax<MethodDeclarationSyntax>(
            privateMethod,
            "void IFace.Method<T>()\r\n{\r\n}");
    }

    [Fact]
    public void TestClassDeclarations()
    {
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c"),
            "class c\r\n{\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", typeParameters: ["x", "y"]),
            "class c<x, y>\r\n{\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", baseType: Generator.IdentifierName("x")),
            "class c : x\r\n{\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", interfaceTypes: [Generator.IdentifierName("x")]),
            "class c : x\r\n{\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", baseType: Generator.IdentifierName("x"), interfaceTypes: [Generator.IdentifierName("y")]),
            "class c : x, y\r\n{\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", interfaceTypes: new SyntaxNode[] { }),
            "class c\r\n{\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", members: [Generator.FieldDeclaration("y", type: Generator.IdentifierName("x"))]),
            "class c\r\n{\r\n    x y;\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", members: [Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"))]),
            "class c\r\n{\r\n    t m()\r\n    {\r\n    }\r\n}");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("c", members: [Generator.ConstructorDeclaration()]),
            "class c\r\n{\r\n    c()\r\n    {\r\n    }\r\n}");
    }

    [Fact]
    public void TestStructDeclarations()
    {
        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s"),
            "struct s\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", typeParameters: ["x", "y"]),
            "struct s<x, y>\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", interfaceTypes: [Generator.IdentifierName("x")]),
            "struct s : x\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", interfaceTypes: [Generator.IdentifierName("x"), Generator.IdentifierName("y")]),
            "struct s : x, y\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", interfaceTypes: new SyntaxNode[] { }),
            "struct s\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", members: [Generator.FieldDeclaration("y", Generator.IdentifierName("x"))]),
            "struct s\r\n{\r\n    x y;\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", members: [Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"))]),
            "struct s\r\n{\r\n    t m()\r\n    {\r\n    }\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.StructDeclaration("s", members: [Generator.ConstructorDeclaration("xxx")]),
            "struct s\r\n{\r\n    s()\r\n    {\r\n    }\r\n}");
    }

    [Fact]
    public void TestInterfaceDeclarations()
    {
        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i"),
            "interface i\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", typeParameters: ["x", "y"]),
            "interface i<x, y>\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", interfaceTypes: [Generator.IdentifierName("a")]),
            "interface i : a\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", interfaceTypes: [Generator.IdentifierName("a"), Generator.IdentifierName("b")]),
            "interface i : a, b\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", interfaceTypes: new SyntaxNode[] { }),
            "interface i\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Sealed)]),
            "interface i\r\n{\r\n    t m();\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Sealed)]),
            "interface i\r\n{\r\n    t p { get; set; }\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.ReadOnly)]),
            "interface i\r\n{\r\n    t p { get; }\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.IndexerDeclaration([Generator.ParameterDeclaration("y", Generator.IdentifierName("x"))], Generator.IdentifierName("t"), Accessibility.Public, DeclarationModifiers.Sealed)]),
            "interface i\r\n{\r\n    t this[x y] { get; set; }\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.IndexerDeclaration([Generator.ParameterDeclaration("y", Generator.IdentifierName("x"))], Generator.IdentifierName("t"), Accessibility.Public, DeclarationModifiers.ReadOnly)]),
            "interface i\r\n{\r\n    t this[x y] { get; }\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Static)]),
            "interface i\r\n{\r\n    event t ep;\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.EventDeclaration("ef", Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Static)]),
            "interface i\r\n{\r\n    event t ef;\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InterfaceDeclaration("i", members: [Generator.FieldDeclaration("f", Generator.IdentifierName("t"), accessibility: Accessibility.Public, modifiers: DeclarationModifiers.Sealed)]),
            "interface i\r\n{\r\n    t f { get; set; }\r\n}");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66377")]
    public void TestInterfaceVariance()
    {
        var compilation = Compile("""
            interface I<in X, out Y> { }
            """);

        var symbol = compilation.GlobalNamespace.GetMembers("I").Single();

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.Declaration(symbol),
            "internal interface I<in X, out Y>\r\n{\r\n}");
    }

    [Fact]
    public void TestEnumDeclarations()
    {
        VerifySyntax<EnumDeclarationSyntax>(
            Generator.EnumDeclaration("e"),
            "enum e\r\n{\r\n}");

        VerifySyntax<EnumDeclarationSyntax>(
            Generator.EnumDeclaration("e", members: [Generator.EnumMember("a"), Generator.EnumMember("b"), Generator.EnumMember("c")]),
            "enum e\r\n{\r\n    a,\r\n    b,\r\n    c\r\n}");

        VerifySyntax<EnumDeclarationSyntax>(
            Generator.EnumDeclaration("e", members: [Generator.IdentifierName("a"), Generator.EnumMember("b"), Generator.IdentifierName("c")]),
            "enum e\r\n{\r\n    a,\r\n    b,\r\n    c\r\n}");

        VerifySyntax<EnumDeclarationSyntax>(
            Generator.EnumDeclaration("e", members: [Generator.EnumMember("a", Generator.LiteralExpression(0)), Generator.EnumMember("b"), Generator.EnumMember("c", Generator.LiteralExpression(5))]),
            "enum e\r\n{\r\n    a = 0,\r\n    b,\r\n    c = 5\r\n}");

        VerifySyntax<EnumDeclarationSyntax>(
            Generator.EnumDeclaration("e", members: [Generator.FieldDeclaration("a", Generator.IdentifierName("e"), initializer: Generator.LiteralExpression(1))]),
            "enum e\r\n{\r\n    a = 1\r\n}");
    }

    [Fact]
    public void TestDelegateDeclarations()
    {
        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d"),
            "delegate void d();");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d", returnType: Generator.IdentifierName("t")),
            "delegate t d();");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d", returnType: Generator.IdentifierName("t"), parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("pt"))]),
            "delegate t d(pt p);");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d", accessibility: Accessibility.Public),
            "public delegate void d();");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d", accessibility: Accessibility.Public),
            "public delegate void d();");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d", modifiers: DeclarationModifiers.New),
            "new delegate void d();");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.DelegateDeclaration("d", typeParameters: ["T", "S"]),
            "delegate void d<T, S>();");
    }

    [Fact]
    public void TestNamespaceImportDeclarations()
    {
        VerifySyntax<UsingDirectiveSyntax>(
            Generator.NamespaceImportDeclaration(Generator.IdentifierName("n")),
            "using n;");

        VerifySyntax<UsingDirectiveSyntax>(
            Generator.NamespaceImportDeclaration("n"),
            "using n;");

        VerifySyntax<UsingDirectiveSyntax>(
            Generator.NamespaceImportDeclaration("n.m"),
            "using n.m;");
    }

    [Fact]
    public void TestNamespaceDeclarations()
    {
        VerifySyntax<NamespaceDeclarationSyntax>(
            Generator.NamespaceDeclaration("n"),
            "namespace n\r\n{\r\n}");

        VerifySyntax<NamespaceDeclarationSyntax>(
            Generator.NamespaceDeclaration("n.m"),
            "namespace n.m\r\n{\r\n}");

        VerifySyntax<NamespaceDeclarationSyntax>(
            Generator.NamespaceDeclaration("n",
                Generator.NamespaceImportDeclaration("m")),
            "namespace n\r\n{\r\n    using m;\r\n}");

        VerifySyntax<NamespaceDeclarationSyntax>(
            Generator.NamespaceDeclaration("n",
                Generator.ClassDeclaration("c"),
                Generator.NamespaceImportDeclaration("m")),
            "namespace n\r\n{\r\n    using m;\r\n\r\n    class c\r\n    {\r\n    }\r\n}");
    }

    [Fact]
    public void TestCompilationUnits()
    {
        VerifySyntax<CompilationUnitSyntax>(
            Generator.CompilationUnit(),
            "");

        VerifySyntax<CompilationUnitSyntax>(
            Generator.CompilationUnit(
                Generator.NamespaceDeclaration("n")),
            "namespace n\r\n{\r\n}");

        VerifySyntax<CompilationUnitSyntax>(
            Generator.CompilationUnit(
                Generator.NamespaceImportDeclaration("n")),
            "using n;");

        VerifySyntax<CompilationUnitSyntax>(
            Generator.CompilationUnit(
                Generator.ClassDeclaration("c"),
                Generator.NamespaceImportDeclaration("m")),
            "using m;\r\n\r\nclass c\r\n{\r\n}");

        VerifySyntax<CompilationUnitSyntax>(
            Generator.CompilationUnit(
                Generator.NamespaceImportDeclaration("n"),
                Generator.NamespaceDeclaration("n",
                    Generator.NamespaceImportDeclaration("m"),
                    Generator.ClassDeclaration("c"))),
            "using n;\r\n\r\nnamespace n\r\n{\r\n    using m;\r\n\r\n    class c\r\n    {\r\n    }\r\n}");
    }

    [Fact]
    public void TestAttributeDeclarations()
    {
        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute(Generator.IdentifierName("a")),
            "[a]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a"),
            "[a]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a.b"),
            "[a.b]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a", []),
            "[a()]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a", [Generator.IdentifierName("x")]),
            "[a(x)]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a", [Generator.AttributeArgument(Generator.IdentifierName("x"))]),
            "[a(x)]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a", [Generator.AttributeArgument("x", Generator.IdentifierName("y"))]),
            "[a(x = y)]");

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute("a", [Generator.IdentifierName("x"), Generator.IdentifierName("y")]),
            "[a(x, y)]");
    }

    [Fact]
    public void TestAddAttributes()
    {
        VerifySyntax<FieldDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.FieldDeclaration("y", Generator.IdentifierName("x")),
                Generator.Attribute("a")),
            "[a]\r\nx y;");

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.AddAttributes(
                    Generator.FieldDeclaration("y", Generator.IdentifierName("x")),
                    Generator.Attribute("a")),
                Generator.Attribute("b")),
            "[a]\r\n[b]\r\nx y;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract),
                Generator.Attribute("a")),
            "[a]\r\nabstract t m();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.AddReturnAttributes(
                Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract),
                Generator.Attribute("a")),
            "[return: a]\r\nabstract t m();");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("x"), accessibility: Accessibility.NotApplicable, modifiers: DeclarationModifiers.Abstract),
                Generator.Attribute("a")),
            "[a]\r\nabstract x p { get; set; }");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.IndexerDeclaration([Generator.ParameterDeclaration("z", Generator.IdentifierName("y"))], Generator.IdentifierName("x"), modifiers: DeclarationModifiers.Abstract),
                Generator.Attribute("a")),
            "[a]\r\nabstract x this[y z] { get; set; }");

        VerifySyntax<EventDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract),
                Generator.Attribute("a")),
            "[a]\r\nabstract event t ep { add; remove; }");

        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.EventDeclaration("ef", Generator.IdentifierName("t")),
                Generator.Attribute("a")),
            "[a]\r\nevent t ef;");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.ClassDeclaration("c"),
                Generator.Attribute("a")),
            "[a]\r\nclass c\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.StructDeclaration("s"),
                Generator.Attribute("a")),
            "[a]\r\nstruct s\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.InterfaceDeclaration("i"),
                Generator.Attribute("a")),
            "[a]\r\ninterface i\r\n{\r\n}");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.AddAttributes(
                Generator.DelegateDeclaration("d"),
                Generator.Attribute("a")),
            "[a]\r\ndelegate void d();");

        VerifySyntax<ParameterSyntax>(
            Generator.AddAttributes(
                Generator.ParameterDeclaration("p", Generator.IdentifierName("t")),
                Generator.Attribute("a")),
            "[a] t p");

        VerifySyntax<CompilationUnitSyntax>(
            Generator.AddAttributes(
                Generator.CompilationUnit(Generator.NamespaceDeclaration("n")),
                Generator.Attribute("a")),
            "[assembly: a]\r\nnamespace n\r\n{\r\n}");

        VerifySyntax<CompilationUnitSyntax>(
            Generator.AddAttributes(
                Generator.AddAttributes(
                    Generator.CompilationUnit(Generator.NamespaceDeclaration("n")),
                    Generator.Attribute("a")),
                Generator.Attribute("b")),
            "[assembly: a]\r\n[assembly: b]\r\nnamespace n\r\n{\r\n}");

        VerifySyntax<StatementSyntax>(
            Generator.AddAttributes(
                BreakStatement(),
                Generator.Attribute("a")),
            "[a]\r\nbreak;");

        VerifySyntax<TypeParameterSyntax>(
            Generator.AddAttributes(
                TypeParameter("T"),
                Generator.Attribute("a")),
            "[a]\r\nT");

        VerifySyntax<LambdaExpressionSyntax>(
            Generator.AddAttributes(
                ParenthesizedLambdaExpression(),
                Generator.Attribute("a")),
            "[a]\r\n() =>");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/5066")]
    public void TestAddAttributesToAccessors()
    {
        var prop = Generator.PropertyDeclaration("P", Generator.IdentifierName("T"));
        var evnt = Generator.CustomEventDeclaration("E", Generator.IdentifierName("T"));
        CheckAddRemoveAttribute(Generator.GetAccessor(prop, DeclarationKind.GetAccessor));
        CheckAddRemoveAttribute(Generator.GetAccessor(prop, DeclarationKind.SetAccessor));
        CheckAddRemoveAttribute(Generator.GetAccessor(evnt, DeclarationKind.AddAccessor));
        CheckAddRemoveAttribute(Generator.GetAccessor(evnt, DeclarationKind.RemoveAccessor));
    }

    private void CheckAddRemoveAttribute(SyntaxNode declaration)
    {
        var initialAttributes = Generator.GetAttributes(declaration);
        Assert.Empty(initialAttributes);

        var withAttribute = Generator.AddAttributes(declaration, Generator.Attribute("a"));
        var attrsAdded = Generator.GetAttributes(withAttribute);
        Assert.Equal(1, attrsAdded.Count);

        var withoutAttribute = Generator.RemoveNode(withAttribute, attrsAdded[0]);
        var attrsRemoved = Generator.GetAttributes(withoutAttribute);
        Assert.Empty(attrsRemoved);
    }

    [Fact]
    public void TestAddRemoveAttributesPerservesTrivia()
    {
        var cls = ParseCompilationUnit("""
            // comment
            public class C { } // end
            """).Members[0];

        var added = Generator.AddAttributes(cls, Generator.Attribute("a"));
        VerifySyntax<ClassDeclarationSyntax>(added, "// comment\r\n[a]\r\npublic class C\r\n{\r\n} // end\r\n");

        var removed = Generator.RemoveAllAttributes(added);
        VerifySyntax<ClassDeclarationSyntax>(removed, "// comment\r\npublic class C\r\n{\r\n} // end\r\n");

        var attrWithComment = Generator.GetAttributes(added).First();
        VerifySyntax<AttributeListSyntax>(attrWithComment, "// comment\r\n[a]");
    }

    [Fact]
    public void TestWithTypeParameters()
    {
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract),
                "a"),
        "abstract void m<a>();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract)),
        "abstract void m();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract),
                "a", "b"),
        "abstract void m<a, b>();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeParameters(Generator.WithTypeParameters(
                Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract),
                "a", "b")),
        "abstract void m();");

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.ClassDeclaration("c"),
                "a", "b"),
        "class c<a, b>\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.StructDeclaration("s"),
                "a", "b"),
        "struct s<a, b>\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.InterfaceDeclaration("i"),
                "a", "b"),
        "interface i<a, b>\r\n{\r\n}");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.WithTypeParameters(
                Generator.DelegateDeclaration("d"),
                "a", "b"),
        "delegate void d<a, b>();");
    }

    [Fact]
    public void TestWithTypeConstraint()
    {
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", Generator.IdentifierName("b")),
            "abstract void m<a>()\r\n    where a : b;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", Generator.IdentifierName("b"), Generator.IdentifierName("c")),
            "abstract void m<a>()\r\n    where a : b, c;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a"),
            "abstract void m<a>();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", Generator.IdentifierName("b"), Generator.IdentifierName("c")), "a"),
            "abstract void m<a>();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeConstraint(
                    Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a", "x"),
                    "a", Generator.IdentifierName("b"), Generator.IdentifierName("c")),
                "x", Generator.IdentifierName("y")),
            "abstract void m<a, x>()\r\n    where a : b, c where x : y;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", SpecialTypeConstraintKind.Constructor),
            "abstract void m<a>()\r\n    where a : new();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", SpecialTypeConstraintKind.ReferenceType),
            "abstract void m<a>()\r\n    where a : class;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", SpecialTypeConstraintKind.ValueType),
            "abstract void m<a>()\r\n    where a : struct;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", SpecialTypeConstraintKind.ReferenceType | SpecialTypeConstraintKind.Constructor),
            "abstract void m<a>()\r\n    where a : class, new();");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", SpecialTypeConstraintKind.ReferenceType | SpecialTypeConstraintKind.ValueType),
            "abstract void m<a>()\r\n    where a : class;");

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Abstract), "a"),
                "a", SpecialTypeConstraintKind.ReferenceType, Generator.IdentifierName("b"), Generator.IdentifierName("c")),
            "abstract void m<a>()\r\n    where a : class, b, c;");

        // type declarations
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(
                    Generator.ClassDeclaration("c"),
                    "a", "b"),
                "a", Generator.IdentifierName("x")),
        "class c<a, b>\r\n    where a : x\r\n{\r\n}");

        VerifySyntax<StructDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(
                    Generator.StructDeclaration("s"),
                    "a", "b"),
                "a", Generator.IdentifierName("x")),
        "struct s<a, b>\r\n    where a : x\r\n{\r\n}");

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(
                    Generator.InterfaceDeclaration("i"),
                    "a", "b"),
                "a", Generator.IdentifierName("x")),
        "interface i<a, b>\r\n    where a : x\r\n{\r\n}");

        VerifySyntax<DelegateDeclarationSyntax>(
            Generator.WithTypeConstraint(
                Generator.WithTypeParameters(
                    Generator.DelegateDeclaration("d"),
                    "a", "b"),
                "a", Generator.IdentifierName("x")),
        "delegate void d<a, b>()\r\n    where a : x;");
    }

    [Fact]
    public void TestInterfaceDeclarationWithEventFromSymbol()
    {
        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.Declaration(_emptyCompilation.GetTypeByMetadataName("System.ComponentModel.INotifyPropertyChanged")),
            """
            public interface INotifyPropertyChanged
            {
                event global::System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/38379")]
    public void TestUnsafeFieldDeclarationFromSymbol()
    {
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(
                _emptyCompilation.GetTypeByMetadataName("System.IntPtr").GetMembers("ToPointer").Single()),
            """
            public unsafe void* ToPointer()
            {
            }
            """);
    }

    [Fact]
    public void TestEnumDeclarationFromSymbol()
    {
        VerifySyntax<EnumDeclarationSyntax>(
                Generator.Declaration(
                    _emptyCompilation.GetTypeByMetadataName("System.DateTimeKind")),
                """
                public enum DateTimeKind
                {
                    Unspecified = 0,
                    Utc = 1,
                    Local = 2
                }
                """);
    }

    [Fact]
    public void TestEnumWithUnderlyingTypeFromSymbol()
    {
        VerifySyntax<EnumDeclarationSyntax>(
                Generator.Declaration(
                    _emptyCompilation.GetTypeByMetadataName("System.Security.SecurityRuleSet")),
                """
                public enum SecurityRuleSet : byte
                {
                    None = 0,
                    Level1 = 1,
                    Level2 = 2
                }
                """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66381")]
    public void TestDelegateDeclarationFromSymbol()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            public delegate void D();
            """));

        var type = compilation.GetTypeByMetadataName("D");

        VerifySyntax<DelegateDeclarationSyntax>(Generator.Declaration(type), """
            public delegate void D();
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65638")]
    public void TestMethodDeclarationFromSymbol1()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                void M(int i = int.MaxValue) { }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var method = type.GetMembers("M").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(method), """
            private void M(global::System.Int32 i = global::System.Int32.MaxValue)
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65835")]
    public void TestMethodDeclarationFromSymbol2()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                void M(params int[] arr) { }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var method = type.GetMembers("M").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(method), """
            private void M(params global::System.Int32[] arr)
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65835")]
    public void TestMethodDeclarationFromSymbol3()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            static class C
            {
                static void M(this int i) { }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var method = type.GetMembers("M").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(method), """
            private static void M(this global::System.Int32 i)
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65835")]
    public void TestMethodDeclarationFromSymbol4()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            static class C
            {
                static void M(this ref int i) { }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var method = type.GetMembers("M").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(method), """
            private static void M(this ref global::System.Int32 i)
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65638")]
    public void TestConstructorDeclarationFromSymbol1()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                public C(int i = int.MaxValue) { }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var method = type.GetMembers(WellKnownMemberNames.InstanceConstructorName).Single();

        VerifySyntax<ConstructorDeclarationSyntax>(
            Generator.Declaration(method),
            """
            public C(global::System.Int32 i = global::System.Int32.MaxValue)
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66379")]
    public void TestPropertyDeclarationFromSymbol1()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                public int Prop { get; protected set; }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var property = type.GetMembers("Prop").Single();

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.Declaration(property),
            "public global::System.Int32 Prop { get; protected set; }");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66379")]
    public void TestPropertyDeclarationFromSymbol2()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                public int Prop { protected get; set; }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var property = type.GetMembers("Prop").Single();

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.Declaration(property),
            "public global::System.Int32 Prop { protected get; set; }");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66382")]
    public void TestOverrideDefaultConstraint1()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            public abstract partial class A
            {
                public abstract TResult? Accept<TResult>(int a);
            }

            public sealed partial class B : A
            {
                public override TResult? Accept<TResult>(int a) where TResult : default { throw null; }
            }
            """));

        var type = compilation.GetTypeByMetadataName("B");
        var property = type.GetMembers("Accept").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(property),
            """
            public override TResult? Accept<TResult>(global::System.Int32 a)
                where TResult : default
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66382")]
    public void TestOverrideDefaultConstraint2()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            public abstract partial class A
            {
                public abstract TResult? Accept<TResult>(int a) where TResult : class;
            }

            public sealed partial class B : A
            {
                public override TResult? Accept<TResult>(int a) where TResult : class { throw null; }
            }
            """));

        var type = compilation.GetTypeByMetadataName("B");
        var property = type.GetMembers("Accept").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(property),
            """
            public override TResult? Accept<TResult>(global::System.Int32 a)
                where TResult : class
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66382")]
    public void TestOverrideDefaultConstraint3()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            public abstract partial class A
            {
                public abstract TResult? Accept<TResult>(int a) where TResult : struct;
            }

            public sealed partial class B : A
            {
                public override TResult? Accept<TResult>(int a) { throw null; }
            }
            """));

        var type = compilation.GetTypeByMetadataName("B");
        var property = type.GetMembers("Accept").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(property),
            """
            public override TResult? Accept<TResult>(global::System.Int32 a)
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66382")]
    public void TestOverrideDefaultConstraint4()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            public class X
            {
            }

            public abstract partial class A
            {
                public abstract TResult? Accept<TResult>(int a) where TResult : X;
            }

            public sealed partial class B : A
            {
                public override TResult? Accept<TResult>(int a) where TResult : class { throw null; }
            }
            """));

        var type = compilation.GetTypeByMetadataName("B");
        var property = type.GetMembers("Accept").Single();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(property),
            """
            public override TResult? Accept<TResult>(global::System.Int32 a)
                where TResult : class
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66375")]
    public void TestExplicitInterface1()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            public interface IGoo
            {
                void BarMethod();
                int BarProp { get; }
                int this[int x] { get; }
                event System.Action E;
            }

            public class Goo : IGoo
            {
                void IGoo.BarMethod() { }
                int IGoo.BarProp => 0;
                int IGoo.this[int x] => 0;
                event System.Action IGoo.E { add { } remove { } }
            }
            """));

        var type = compilation.GetTypeByMetadataName("Goo");
        var method = type.GetMembers().Single(m => m is IMethodSymbol { MethodKind: MethodKind.ExplicitInterfaceImplementation });
        var property = type.GetMembers().Single(m => m is IPropertySymbol { IsIndexer: false });
        var indexer = type.GetMembers().Single(m => m is IPropertySymbol { IsIndexer: true });
        var ev = type.GetMembers().Single(m => m is IEventSymbol);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(method),
            """
            void global::IGoo.BarMethod()
            {
            }
            """);
        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.Declaration(property),
            "global::System.Int32 global::IGoo.BarProp { get; }");
        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.Declaration(indexer),
            """
            global::System.Int32 global::IGoo.this[global::System.Int32 x]
            {
                get
                {
                }
            }
            """);
        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.Declaration(ev),
            """
            event global::System.Action IGoo.E;
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66380")]
    public void TestConstantFieldDeclarations()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                public const int F;
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var field = type.GetMembers("F").Single();

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.Declaration(field),
            "public const global::System.Int32 F;");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69376")]
    public void TestConstantDecimalFieldDeclarationFromMetadata()
    {
        var compilation = _emptyCompilation.
            WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)).
            AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                public const decimal F = 8675309000000M;
            }
            """));
        var reference = compilation.EmitToPortableExecutableReference();

        compilation = _emptyCompilation.AddReferences(reference);

        var type = compilation.GetTypeByMetadataName("C");
        var field = type.GetMembers("F").Single();

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.Declaration(field),
            "public const global::System.Decimal F = 8675309000000M;");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/69380")]
    public void TestConstantFieldDeclarationSpecialTypes()
    {
        var field = _emptyCompilation.GetSpecialType(SpecialType.System_UInt32).GetMembers(nameof(UInt32.MaxValue)).Single();

        VerifySyntax<FieldDeclarationSyntax>(
            Generator.Declaration(field),
            "public const global::System.UInt32 MaxValue = 4294967295U;");
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66374")]
    public void TestDestructor1()
    {
        var compilation = _emptyCompilation.AddSyntaxTrees(ParseSyntaxTree("""
            class C
            {
                ~C()
                {
                }
            }
            """));

        var type = compilation.GetTypeByMetadataName("C");
        var method = type.GetMembers(WellKnownMemberNames.DestructorName).Single();

        VerifySyntax<DestructorDeclarationSyntax>(
            Generator.Declaration(method), """
            ~C()
            {
            }
            """);
    }

    #endregion

    #region Add/Insert/Remove/Get declarations & members/elements

    private void AssertNamesEqual(string[] expectedNames, IEnumerable<SyntaxNode> actualNodes)
    {
        var actualNames = actualNodes.Select(n => Generator.GetName(n)).ToArray();
        var expected = string.Join(", ", expectedNames);
        var actual = string.Join(", ", actualNames);
        Assert.Equal(expected, actual);
    }

    private void AssertNamesEqual(string name, IEnumerable<SyntaxNode> actualNodes)
        => AssertNamesEqual([name], actualNodes);

    private void AssertMemberNamesEqual(string[] expectedNames, SyntaxNode declaration)
        => AssertNamesEqual(expectedNames, Generator.GetMembers(declaration));

    private void AssertMemberNamesEqual(string expectedName, SyntaxNode declaration)
        => AssertNamesEqual([expectedName], Generator.GetMembers(declaration));

    [Fact]
    public void TestAddNamespaceImports()
    {
        AssertNamesEqual("x.y", Generator.GetNamespaceImports(Generator.AddNamespaceImports(Generator.CompilationUnit(), Generator.NamespaceImportDeclaration("x.y"))));
        AssertNamesEqual(["x.y", "z"], Generator.GetNamespaceImports(Generator.AddNamespaceImports(Generator.CompilationUnit(), Generator.NamespaceImportDeclaration("x.y"), Generator.IdentifierName("z"))));
        AssertNamesEqual("", Generator.GetNamespaceImports(Generator.AddNamespaceImports(Generator.CompilationUnit(), Generator.MethodDeclaration("m"))));
        AssertNamesEqual(["x", "y.z"], Generator.GetNamespaceImports(Generator.AddNamespaceImports(Generator.CompilationUnit(Generator.IdentifierName("x")), Generator.DottedName("y.z"))));
    }

    [Fact]
    public void TestRemoveNamespaceImports()
    {
        TestRemoveAllNamespaceImports(Generator.CompilationUnit(Generator.NamespaceImportDeclaration("x")));
        TestRemoveAllNamespaceImports(Generator.CompilationUnit(Generator.NamespaceImportDeclaration("x"), Generator.IdentifierName("y")));

        TestRemoveNamespaceImport(Generator.CompilationUnit(Generator.NamespaceImportDeclaration("x")), "x", []);
        TestRemoveNamespaceImport(Generator.CompilationUnit(Generator.NamespaceImportDeclaration("x"), Generator.IdentifierName("y")), "x", ["y"]);
        TestRemoveNamespaceImport(Generator.CompilationUnit(Generator.NamespaceImportDeclaration("x"), Generator.IdentifierName("y")), "y", ["x"]);
    }

    private void TestRemoveAllNamespaceImports(SyntaxNode declaration)
        => Assert.Equal(0, Generator.GetNamespaceImports(Generator.RemoveNodes(declaration, Generator.GetNamespaceImports(declaration))).Count);

    private void TestRemoveNamespaceImport(SyntaxNode declaration, string name, string[] remainingNames)
    {
        var newDecl = Generator.RemoveNode(declaration, Generator.GetNamespaceImports(declaration).First(m => Generator.GetName(m) == name));
        AssertNamesEqual(remainingNames, Generator.GetNamespaceImports(newDecl));
    }

    [Fact]
    public void TestRemoveNodeInTrivia()
    {
        var code = """
            ///<summary> ... </summary>
            public class C
            {
            }
            """;

        var cu = ParseCompilationUnit(code);
        var cls = cu.Members[0];
        var summary = cls.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().First();

        var newCu = Generator.RemoveNode(cu, summary);

        VerifySyntaxRaw<CompilationUnitSyntax>(
            newCu,
            """

            public class C
            {
            }
            """);
    }

    [Fact]
    public void TestReplaceNodeInTrivia()
    {
        var code = """
            ///<summary> ... </summary>
            public class C
            {
            }
            """;

        var cu = ParseCompilationUnit(code);
        var cls = cu.Members[0];
        var summary = cls.DescendantNodes(descendIntoTrivia: true).OfType<XmlElementSyntax>().First();

        var summary2 = summary.WithContent(default);

        var newCu = Generator.ReplaceNode(cu, summary, summary2);

        VerifySyntaxRaw<CompilationUnitSyntax>(
            newCu, """
            ///<summary></summary>
            public class C
            {
            }
            """);
    }

    [Fact]
    public void TestInsertAfterNodeInTrivia()
    {
        var code = """
            ///<summary> ... </summary>
            public class C
            {
            }
            """;

        var cu = ParseCompilationUnit(code);
        var cls = cu.Members[0];
        var text = cls.DescendantNodes(descendIntoTrivia: true).OfType<XmlTextSyntax>().First();

        var newCu = Generator.InsertNodesAfter(cu, text, [text]);

        VerifySyntaxRaw<CompilationUnitSyntax>(
            newCu, """
            ///<summary> ...  ... </summary>
            public class C
            {
            }
            """);
    }

    [Fact]
    public void TestInsertBeforeNodeInTrivia()
    {
        var code = """
            ///<summary> ... </summary>
            public class C
            {
            }
            """;

        var cu = ParseCompilationUnit(code);
        var cls = cu.Members[0];
        var text = cls.DescendantNodes(descendIntoTrivia: true).OfType<XmlTextSyntax>().First();

        var newCu = Generator.InsertNodesBefore(cu, text, [text]);

        VerifySyntaxRaw<CompilationUnitSyntax>(
            newCu, """
            ///<summary> ...  ... </summary>
            public class C
            {
            }
            """);
    }

    [Fact]
    public void TestAddMembers()
    {
        AssertMemberNamesEqual("m", Generator.AddMembers(Generator.ClassDeclaration("d"), [Generator.MethodDeclaration("m")]));
        AssertMemberNamesEqual("m", Generator.AddMembers(Generator.StructDeclaration("s"), [Generator.MethodDeclaration("m")]));
        AssertMemberNamesEqual("m", Generator.AddMembers(Generator.InterfaceDeclaration("i"), [Generator.MethodDeclaration("m")]));
        AssertMemberNamesEqual("", Generator.AddMembers(Generator.InterfaceDeclaration("i"), [Generator.OperatorDeclaration(OperatorKind.Addition)]));
        AssertMemberNamesEqual("v", Generator.AddMembers(Generator.EnumDeclaration("e"), [Generator.EnumMember("v")]));
        AssertMemberNamesEqual("n2", Generator.AddMembers(Generator.NamespaceDeclaration("n"), [Generator.NamespaceDeclaration("n2")]));
        AssertMemberNamesEqual("n", Generator.AddMembers(Generator.CompilationUnit(), [Generator.NamespaceDeclaration("n")]));

        AssertMemberNamesEqual(["m", "m2"], Generator.AddMembers(Generator.ClassDeclaration("d", members: [Generator.MethodDeclaration("m")]), [Generator.MethodDeclaration("m2")]));
        AssertMemberNamesEqual(["m", "m2"], Generator.AddMembers(Generator.StructDeclaration("s", members: [Generator.MethodDeclaration("m")]), [Generator.MethodDeclaration("m2")]));
        AssertMemberNamesEqual(["m", "m2"], Generator.AddMembers(Generator.InterfaceDeclaration("i", members: [Generator.MethodDeclaration("m")]), [Generator.MethodDeclaration("m2")]));
        AssertMemberNamesEqual(["v", "v2"], Generator.AddMembers(Generator.EnumDeclaration("i", members: [Generator.EnumMember("v")]), [Generator.EnumMember("v2")]));
        AssertMemberNamesEqual(["n1", "n2"], Generator.AddMembers(Generator.NamespaceDeclaration("n", [Generator.NamespaceDeclaration("n1")]), [Generator.NamespaceDeclaration("n2")]));
        AssertMemberNamesEqual(["n1", "n2"], Generator.AddMembers(Generator.CompilationUnit(declarations: [Generator.NamespaceDeclaration("n1")]), [Generator.NamespaceDeclaration("n2")]));
    }

    [Fact]
    public void TestAddOperatorMembersToInterface()
    {
        VerifySyntax<InterfaceDeclarationSyntax>(Generator.AddMembers(Generator.InterfaceDeclaration("i"),
                [Generator.OperatorDeclaration(OperatorKind.Addition)]),
            """
            interface i
            {
                static void operator +();
            }
            """);

        VerifySyntax<InterfaceDeclarationSyntax>(Generator.AddMembers(Generator.InterfaceDeclaration("i"),
                [Generator.OperatorDeclaration(OperatorKind.Addition, modifiers: DeclarationModifiers.Abstract)]),
            """
            interface i
            {
                static abstract void operator +();
            }
            """);

        VerifySyntax<InterfaceDeclarationSyntax>(Generator.AddMembers(Generator.InterfaceDeclaration("i"),
                [Generator.OperatorDeclaration(OperatorKind.Addition, modifiers: DeclarationModifiers.Virtual)]),
            """
            interface i
            {
                static virtual void operator +();
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65932")]
    public void TestAddExpressionBodyMembersToInterface()
    {
        var method = (MethodDeclarationSyntax)Generator.MethodDeclaration("m");
        method = method.WithBody(null).WithSemicolonToken(SemicolonToken);
        method = method.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.IdentifierName("x")));

        VerifySyntax<InterfaceDeclarationSyntax>(Generator.AddMembers(Generator.InterfaceDeclaration("i"),
                [method]),
            """
            interface i
            {
                void m();
            }
            """);

        var getAccessor = (AccessorDeclarationSyntax)Generator.GetAccessorDeclaration();
        getAccessor = getAccessor.WithBody(null).WithSemicolonToken(SemicolonToken);
        getAccessor = getAccessor.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.IdentifierName("x")));

        var setAccessor = (AccessorDeclarationSyntax)Generator.SetAccessorDeclaration();
        setAccessor = setAccessor.WithBody(null).WithSemicolonToken(SemicolonToken);
        setAccessor = setAccessor.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.InvocationExpression(Generator.IdentifierName("x"))));

        var property = (PropertyDeclarationSyntax)
            Generator.WithAccessorDeclarations(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("x")),
                [getAccessor, setAccessor]);

        VerifySyntax<InterfaceDeclarationSyntax>(Generator.AddMembers(Generator.InterfaceDeclaration("i"),
                [property]),
            """
            interface i
            {
                x p { get; set; }
            }
            """);
    }

    [Fact]
    public void TestRemoveMembers()
    {
        // remove all members
        TestRemoveAllMembers(Generator.ClassDeclaration("c", members: [Generator.MethodDeclaration("m")]));
        TestRemoveAllMembers(Generator.StructDeclaration("s", members: [Generator.MethodDeclaration("m")]));
        TestRemoveAllMembers(Generator.InterfaceDeclaration("i", members: [Generator.MethodDeclaration("m")]));
        TestRemoveAllMembers(Generator.EnumDeclaration("i", members: [Generator.EnumMember("v")]));
        TestRemoveAllMembers(Generator.NamespaceDeclaration("n", [Generator.NamespaceDeclaration("n")]));
        TestRemoveAllMembers(Generator.CompilationUnit(declarations: [Generator.NamespaceDeclaration("n")]));

        TestRemoveMember(Generator.ClassDeclaration("c", members: [Generator.MethodDeclaration("m1"), Generator.MethodDeclaration("m2")]), "m1", ["m2"]);
        TestRemoveMember(Generator.StructDeclaration("s", members: [Generator.MethodDeclaration("m1"), Generator.MethodDeclaration("m2")]), "m1", ["m2"]);
    }

    private void TestRemoveAllMembers(SyntaxNode declaration)
        => Assert.Equal(0, Generator.GetMembers(Generator.RemoveNodes(declaration, Generator.GetMembers(declaration))).Count);

    private void TestRemoveMember(SyntaxNode declaration, string name, string[] remainingNames)
    {
        var newDecl = Generator.RemoveNode(declaration, Generator.GetMembers(declaration).First(m => Generator.GetName(m) == name));
        AssertMemberNamesEqual(remainingNames, newDecl);
    }

    [Fact]
    public void TestGetMembers()
    {
        AssertMemberNamesEqual("m", Generator.ClassDeclaration("c", members: [Generator.MethodDeclaration("m")]));
        AssertMemberNamesEqual("m", Generator.StructDeclaration("s", members: [Generator.MethodDeclaration("m")]));
        AssertMemberNamesEqual("m", Generator.InterfaceDeclaration("i", members: [Generator.MethodDeclaration("m")]));
        AssertMemberNamesEqual("v", Generator.EnumDeclaration("e", members: [Generator.EnumMember("v")]));
        AssertMemberNamesEqual("c", Generator.NamespaceDeclaration("n", declarations: [Generator.ClassDeclaration("c")]));
        AssertMemberNamesEqual("c", Generator.CompilationUnit(declarations: [Generator.ClassDeclaration("c")]));
    }

    [Fact]
    public void TestGetDeclarationKind()
    {
        Assert.Equal(DeclarationKind.CompilationUnit, Generator.GetDeclarationKind(Generator.CompilationUnit()));
        Assert.Equal(DeclarationKind.Class, Generator.GetDeclarationKind(Generator.ClassDeclaration("c")));
        Assert.Equal(DeclarationKind.Struct, Generator.GetDeclarationKind(Generator.StructDeclaration("s")));
        Assert.Equal(DeclarationKind.Interface, Generator.GetDeclarationKind(Generator.InterfaceDeclaration("i")));
        Assert.Equal(DeclarationKind.Enum, Generator.GetDeclarationKind(Generator.EnumDeclaration("e")));
        Assert.Equal(DeclarationKind.Delegate, Generator.GetDeclarationKind(Generator.DelegateDeclaration("d")));
        Assert.Equal(DeclarationKind.Method, Generator.GetDeclarationKind(Generator.MethodDeclaration("m")));
        Assert.Equal(DeclarationKind.Constructor, Generator.GetDeclarationKind(Generator.ConstructorDeclaration()));
        Assert.Equal(DeclarationKind.Parameter, Generator.GetDeclarationKind(Generator.ParameterDeclaration("p")));
        Assert.Equal(DeclarationKind.Property, Generator.GetDeclarationKind(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"))));
        Assert.Equal(DeclarationKind.Indexer, Generator.GetDeclarationKind(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t"))));
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(Generator.FieldDeclaration("f", Generator.IdentifierName("t"))));
        Assert.Equal(DeclarationKind.EnumMember, Generator.GetDeclarationKind(Generator.EnumMember("v")));
        Assert.Equal(DeclarationKind.Event, Generator.GetDeclarationKind(Generator.EventDeclaration("ef", Generator.IdentifierName("t"))));
        Assert.Equal(DeclarationKind.CustomEvent, Generator.GetDeclarationKind(Generator.CustomEventDeclaration("e", Generator.IdentifierName("t"))));
        Assert.Equal(DeclarationKind.Namespace, Generator.GetDeclarationKind(Generator.NamespaceDeclaration("n")));
        Assert.Equal(DeclarationKind.NamespaceImport, Generator.GetDeclarationKind(Generator.NamespaceImportDeclaration("u")));
        Assert.Equal(DeclarationKind.Variable, Generator.GetDeclarationKind(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc")));
        Assert.Equal(DeclarationKind.Attribute, Generator.GetDeclarationKind(Generator.Attribute("a")));
    }

    [Fact]
    public void TestGetName()
    {
        Assert.Equal("c", Generator.GetName(Generator.ClassDeclaration("c")));
        Assert.Equal("s", Generator.GetName(Generator.StructDeclaration("s")));
        Assert.Equal("i", Generator.GetName(Generator.EnumDeclaration("i")));
        Assert.Equal("e", Generator.GetName(Generator.EnumDeclaration("e")));
        Assert.Equal("d", Generator.GetName(Generator.DelegateDeclaration("d")));
        Assert.Equal("m", Generator.GetName(Generator.MethodDeclaration("m")));
        Assert.Equal("", Generator.GetName(Generator.ConstructorDeclaration()));
        Assert.Equal("p", Generator.GetName(Generator.ParameterDeclaration("p")));
        Assert.Equal("p", Generator.GetName(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"))));
        Assert.Equal("", Generator.GetName(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t"))));
        Assert.Equal("f", Generator.GetName(Generator.FieldDeclaration("f", Generator.IdentifierName("t"))));
        Assert.Equal("v", Generator.GetName(Generator.EnumMember("v")));
        Assert.Equal("ef", Generator.GetName(Generator.EventDeclaration("ef", Generator.IdentifierName("t"))));
        Assert.Equal("ep", Generator.GetName(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"))));
        Assert.Equal("n", Generator.GetName(Generator.NamespaceDeclaration("n")));
        Assert.Equal("u", Generator.GetName(Generator.NamespaceImportDeclaration("u")));
        Assert.Equal("loc", Generator.GetName(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc")));
        Assert.Equal("a", Generator.GetName(Generator.Attribute("a")));
    }

    [Fact]
    public void TestWithName()
    {
        Assert.Equal("c", Generator.GetName(Generator.WithName(Generator.ClassDeclaration("x"), "c")));
        Assert.Equal("s", Generator.GetName(Generator.WithName(Generator.StructDeclaration("x"), "s")));
        Assert.Equal("i", Generator.GetName(Generator.WithName(Generator.EnumDeclaration("x"), "i")));
        Assert.Equal("e", Generator.GetName(Generator.WithName(Generator.EnumDeclaration("x"), "e")));
        Assert.Equal("d", Generator.GetName(Generator.WithName(Generator.DelegateDeclaration("x"), "d")));
        Assert.Equal("m", Generator.GetName(Generator.WithName(Generator.MethodDeclaration("x"), "m")));
        Assert.Equal("", Generator.GetName(Generator.WithName(Generator.ConstructorDeclaration(), ".ctor")));
        Assert.Equal("p", Generator.GetName(Generator.WithName(Generator.ParameterDeclaration("x"), "p")));
        Assert.Equal("p", Generator.GetName(Generator.WithName(Generator.PropertyDeclaration("x", Generator.IdentifierName("t")), "p")));
        Assert.Equal("", Generator.GetName(Generator.WithName(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t")), "this")));
        Assert.Equal("f", Generator.GetName(Generator.WithName(Generator.FieldDeclaration("x", Generator.IdentifierName("t")), "f")));
        Assert.Equal("v", Generator.GetName(Generator.WithName(Generator.EnumMember("x"), "v")));
        Assert.Equal("ef", Generator.GetName(Generator.WithName(Generator.EventDeclaration("x", Generator.IdentifierName("t")), "ef")));
        Assert.Equal("ep", Generator.GetName(Generator.WithName(Generator.CustomEventDeclaration("x", Generator.IdentifierName("t")), "ep")));
        Assert.Equal("n", Generator.GetName(Generator.WithName(Generator.NamespaceDeclaration("x"), "n")));
        Assert.Equal("u", Generator.GetName(Generator.WithName(Generator.NamespaceImportDeclaration("x"), "u")));
        Assert.Equal("loc", Generator.GetName(Generator.WithName(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "x"), "loc")));
        Assert.Equal("a", Generator.GetName(Generator.WithName(Generator.Attribute("x"), "a")));
    }

    [Fact]
    public void TestGetAccessibility()
    {
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.ClassDeclaration("c", accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.StructDeclaration("s", accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.EnumDeclaration("i", accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.EnumDeclaration("e", accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.DelegateDeclaration("d", accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.MethodDeclaration("m", accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.ConstructorDeclaration(accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.ParameterDeclaration("p")));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t"), accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.FieldDeclaration("f", Generator.IdentifierName("t"), accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.EnumMember("v")));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.EventDeclaration("ef", Generator.IdentifierName("t"), accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.Internal, Generator.GetAccessibility(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), accessibility: Accessibility.Internal)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.NamespaceDeclaration("n")));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.NamespaceImportDeclaration("u")));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc")));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.Attribute("a")));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(TypeParameter("tp")));
    }

    [Fact]
    public void TestWithAccessibility()
    {
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.ClassDeclaration("c", accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.StructDeclaration("s", accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.EnumDeclaration("i", accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.EnumDeclaration("e", accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.DelegateDeclaration("d", accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.MethodDeclaration("m", accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.ConstructorDeclaration(accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(Generator.ParameterDeclaration("p"), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t"), accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.FieldDeclaration("f", Generator.IdentifierName("t"), accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(Generator.EnumMember("v"), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.EventDeclaration("ef", Generator.IdentifierName("t"), accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), accessibility: Accessibility.Internal), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(Generator.NamespaceDeclaration("n"), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(Generator.NamespaceImportDeclaration("u"), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc"), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(Generator.Attribute("a"), Accessibility.Private)));
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(Generator.WithAccessibility(TypeParameter("tp"), Accessibility.Private)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(AccessorDeclaration(SyntaxKind.InitAccessorDeclaration), Accessibility.Private)));
    }

    [Fact]
    public void TestGetModifiers()
    {
        Assert.Equal(DeclarationModifiers.Abstract, Generator.GetModifiers(Generator.ClassDeclaration("c", modifiers: DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Partial, Generator.GetModifiers(Generator.StructDeclaration("s", modifiers: DeclarationModifiers.Partial)));
        Assert.Equal(DeclarationModifiers.New, Generator.GetModifiers(Generator.EnumDeclaration("e", modifiers: DeclarationModifiers.New)));
        Assert.Equal(DeclarationModifiers.New, Generator.GetModifiers(Generator.DelegateDeclaration("d", modifiers: DeclarationModifiers.New)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.MethodDeclaration("m", modifiers: DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.ConstructorDeclaration(modifiers: DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.ParameterDeclaration("p")));
        Assert.Equal(DeclarationModifiers.Abstract, Generator.GetModifiers(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Abstract, Generator.GetModifiers(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Const, Generator.GetModifiers(Generator.FieldDeclaration("f", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Const)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.EventDeclaration("ef", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"), modifiers: DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.EnumMember("v")));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.NamespaceDeclaration("n")));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.NamespaceImportDeclaration("u")));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc")));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.Attribute("a")));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(TypeParameter("tp")));
    }

    [Fact]
    public void TestWithModifiers()
    {
        Assert.Equal(DeclarationModifiers.Abstract, Generator.GetModifiers(Generator.WithModifiers(Generator.ClassDeclaration("c"), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Partial, Generator.GetModifiers(Generator.WithModifiers(Generator.StructDeclaration("s"), DeclarationModifiers.Partial)));
        Assert.Equal(DeclarationModifiers.New, Generator.GetModifiers(Generator.WithModifiers(Generator.EnumDeclaration("e"), DeclarationModifiers.New)));
        Assert.Equal(DeclarationModifiers.New, Generator.GetModifiers(Generator.WithModifiers(Generator.DelegateDeclaration("d"), DeclarationModifiers.New)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.WithModifiers(Generator.MethodDeclaration("m"), DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.WithModifiers(Generator.ConstructorDeclaration(), DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(Generator.ParameterDeclaration("p"), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Abstract, Generator.GetModifiers(Generator.WithModifiers(Generator.PropertyDeclaration("p", Generator.IdentifierName("t")), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Abstract, Generator.GetModifiers(Generator.WithModifiers(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t")), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.Const, Generator.GetModifiers(Generator.WithModifiers(Generator.FieldDeclaration("f", Generator.IdentifierName("t")), DeclarationModifiers.Const)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.WithModifiers(Generator.EventDeclaration("ef", Generator.IdentifierName("t")), DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(Generator.WithModifiers(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t")), DeclarationModifiers.Static)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(Generator.EnumMember("v"), DeclarationModifiers.Partial)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(Generator.NamespaceDeclaration("n"), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(Generator.NamespaceImportDeclaration("u"), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc"), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(Generator.Attribute("a"), DeclarationModifiers.Abstract)));
        Assert.Equal(DeclarationModifiers.None, Generator.GetModifiers(Generator.WithModifiers(TypeParameter("tp"), DeclarationModifiers.Abstract)));
    }

    [Fact]
    public void TestWithModifiers_AllowedModifiers()
    {
        var allModifiers = new DeclarationModifiers(true, true, true, true, true, true, true, true, true, true, true, true, true);

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.New | DeclarationModifiers.Partial | DeclarationModifiers.Sealed | DeclarationModifiers.Static | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.ClassDeclaration("c"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.New | DeclarationModifiers.Partial | DeclarationModifiers.Unsafe | DeclarationModifiers.ReadOnly,
            Generator.GetModifiers(Generator.WithModifiers(Generator.StructDeclaration("s"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.New | DeclarationModifiers.Partial | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.InterfaceDeclaration("i"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.New | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.DelegateDeclaration("d"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.New,
            Generator.GetModifiers(Generator.WithModifiers(Generator.EnumDeclaration("e"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Const | DeclarationModifiers.New | DeclarationModifiers.ReadOnly | DeclarationModifiers.Static | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.FieldDeclaration("f", Generator.IdentifierName("t")), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Static | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.ConstructorDeclaration("c"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(DestructorDeclaration("c"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.Async | DeclarationModifiers.New | DeclarationModifiers.Override | DeclarationModifiers.Partial | DeclarationModifiers.Sealed | DeclarationModifiers.Static | DeclarationModifiers.Virtual | DeclarationModifiers.Unsafe | DeclarationModifiers.ReadOnly,
            Generator.GetModifiers(Generator.WithModifiers(Generator.MethodDeclaration("m"), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.New | DeclarationModifiers.Override | DeclarationModifiers.ReadOnly | DeclarationModifiers.Sealed | DeclarationModifiers.Static | DeclarationModifiers.Virtual | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.PropertyDeclaration("p", Generator.IdentifierName("t")), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.New | DeclarationModifiers.Override | DeclarationModifiers.ReadOnly | DeclarationModifiers.Sealed | DeclarationModifiers.Static | DeclarationModifiers.Virtual | DeclarationModifiers.Unsafe,
            Generator.GetModifiers(Generator.WithModifiers(Generator.IndexerDeclaration([Generator.ParameterDeclaration("i")], Generator.IdentifierName("t")), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.New | DeclarationModifiers.Override | DeclarationModifiers.Sealed | DeclarationModifiers.Static | DeclarationModifiers.Virtual | DeclarationModifiers.Unsafe | DeclarationModifiers.ReadOnly,
            Generator.GetModifiers(Generator.WithModifiers(Generator.EventDeclaration("ef", Generator.IdentifierName("t")), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.New | DeclarationModifiers.Override | DeclarationModifiers.Sealed | DeclarationModifiers.Static | DeclarationModifiers.Virtual | DeclarationModifiers.Unsafe | DeclarationModifiers.ReadOnly,
            Generator.GetModifiers(Generator.WithModifiers(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t")), allModifiers)));

        Assert.Equal(
            DeclarationModifiers.Abstract | DeclarationModifiers.New | DeclarationModifiers.Override | DeclarationModifiers.Virtual | DeclarationModifiers.ReadOnly,
            Generator.GetModifiers(Generator.WithModifiers(AccessorDeclaration(SyntaxKind.GetAccessorDeclaration), allModifiers)));
    }

    [Fact]
    public void TestAddPublicToStaticConstructor()
    {
        var ctor = Generator.ConstructorDeclaration("C", modifiers: DeclarationModifiers.Static);
        VerifySyntax<ConstructorDeclarationSyntax>(ctor, """
            static C()
            {
            }
            """);

        var publicCtor = Generator.WithAccessibility(ctor, Accessibility.Public);
        VerifySyntax<ConstructorDeclarationSyntax>(publicCtor, """
            public C()
            {
            }
            """);
    }

    [Fact]
    public void TestAddStaticToPublicConstructor()
    {
        var ctor = Generator.ConstructorDeclaration("C", accessibility: Accessibility.Public);
        VerifySyntax<ConstructorDeclarationSyntax>(ctor, """
            public C()
            {
            }
            """);

        var staticCtor = Generator.WithModifiers(ctor, DeclarationModifiers.Static);
        VerifySyntax<ConstructorDeclarationSyntax>(staticCtor, """
            static C()
            {
            }
            """);
    }

    [Fact]
    public void TestAddAbstractToFileClass()
    {
        var fileClass = (ClassDeclarationSyntax)ParseMemberDeclaration("file class C { }");
        var fileAbstractClass = Generator.WithModifiers(fileClass, Generator.GetModifiers(fileClass).WithIsAbstract(true));
        VerifySyntax<ClassDeclarationSyntax>(fileAbstractClass, """
            file abstract class C
            {
            }
            """);
    }

    [Fact]
    public void TestAddPublicToFileClass()
    {
        var fileClass = (ClassDeclarationSyntax)ParseMemberDeclaration("file class C { }");
        var filePublicClass = Generator.WithAccessibility(fileClass, Accessibility.Public);
        VerifySyntax<ClassDeclarationSyntax>(filePublicClass, """
            public class C
            {
            }
            """);
    }

    [Fact]
    public void TestAddFileModifierToAbstractClass()
    {
        var abstractClass = (ClassDeclarationSyntax)ParseMemberDeclaration("abstract class C { }");
        var fileAbstractClass = Generator.WithModifiers(abstractClass, Generator.GetModifiers(abstractClass).WithIsFile(true));
        VerifySyntax<ClassDeclarationSyntax>(fileAbstractClass, """
            file abstract class C
            {
            }
            """);
    }

    [Fact]
    public void TestAddFileModifierToPublicClass()
    {
        var publicClass = (ClassDeclarationSyntax)ParseMemberDeclaration("public class C { }");
        var filePublicClass = Generator.WithModifiers(publicClass, Generator.GetModifiers(publicClass).WithIsFile(true));
        VerifySyntax<ClassDeclarationSyntax>(filePublicClass, """
            file class C
            {
            }
            """);
    }

    [Fact]
    public void TestAddRequiredModifierToVirtualProperty()
    {
        var property = (PropertyDeclarationSyntax)ParseMemberDeclaration("public virtual int P { get; }");
        var updatedProperty = Generator.WithModifiers(property, Generator.GetModifiers(property).WithIsRequired(true));
        VerifySyntax<PropertyDeclarationSyntax>(updatedProperty, "public virtual required int P { get; }");
    }

    [Fact]
    public void TestAddVirtualModifierToRequiredProperty()
    {
        var property = (PropertyDeclarationSyntax)ParseMemberDeclaration("public required int P { get; }");
        var updatedProperty = Generator.WithModifiers(property, Generator.GetModifiers(property).WithIsVirtual(true));
        VerifySyntax<PropertyDeclarationSyntax>(updatedProperty, "public virtual required int P { get; }");
    }

    [Theory, WorkItem("https://github.com/dotnet/roslyn/issues/66295")]
    [InlineData("private protected")]
    [InlineData("protected internal")]
    public void TestCompoundAccessibilityModifierKeywordsOrder(string modifier)
    {
        var property = (PropertyDeclarationSyntax)ParseMemberDeclaration($$"""{{modifier}} int P { get; }""");
        var updatedProperty = Generator.WithModifiers(property, Generator.GetModifiers(property).WithIsRequired(true));
        VerifySyntax<PropertyDeclarationSyntax>(updatedProperty, $$"""{{modifier}} required int P { get; }""");
    }

    [Fact]
    public void TestGetType()
    {
        Assert.Equal("t", Generator.GetType(Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("t"))).ToString());
        Assert.Null(Generator.GetType(Generator.MethodDeclaration("m")));

        Assert.Equal("t", Generator.GetType(Generator.FieldDeclaration("f", Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("pt"))], Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))).ToString());

        Assert.Equal("t", Generator.GetType(Generator.EventDeclaration("ef", Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("t"))).ToString());

        Assert.Equal("t", Generator.GetType(Generator.DelegateDeclaration("t", returnType: Generator.IdentifierName("t"))).ToString());
        Assert.Null(Generator.GetType(Generator.DelegateDeclaration("d")));

        Assert.Equal("t", Generator.GetType(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "v")).ToString());

        Assert.Null(Generator.GetType(Generator.ClassDeclaration("c")));
        Assert.Null(Generator.GetType(Generator.IdentifierName("x")));
    }

    [Fact]
    public void TestWithType()
    {
        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.MethodDeclaration("m", returnType: Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.FieldDeclaration("f", Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.PropertyDeclaration("p", Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("pt"))], Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.ParameterDeclaration("p", Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());

        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.DelegateDeclaration("t"), Generator.IdentifierName("t"))).ToString());

        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.EventDeclaration("ef", Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());
        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.CustomEventDeclaration("ep", Generator.IdentifierName("x")), Generator.IdentifierName("t"))).ToString());

        Assert.Equal("t", Generator.GetType(Generator.WithType(Generator.LocalDeclarationStatement(Generator.IdentifierName("x"), "v"), Generator.IdentifierName("t"))).ToString());
        Assert.Null(Generator.GetType(Generator.WithType(Generator.ClassDeclaration("c"), Generator.IdentifierName("t"))));
        Assert.Null(Generator.GetType(Generator.WithType(Generator.IdentifierName("x"), Generator.IdentifierName("t"))));
    }

    [Fact]
    public void TestGetParameters()
    {
        Assert.Equal(0, Generator.GetParameters(Generator.MethodDeclaration("m")).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.MethodDeclaration("m", parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(2, Generator.GetParameters(Generator.MethodDeclaration("m", parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("t")), Generator.ParameterDeclaration("p2", Generator.IdentifierName("t2"))])).Count);

        Assert.Equal(0, Generator.GetParameters(Generator.ConstructorDeclaration()).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.ConstructorDeclaration(parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(2, Generator.GetParameters(Generator.ConstructorDeclaration(parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("t")), Generator.ParameterDeclaration("p2", Generator.IdentifierName("t2"))])).Count);

        Assert.Equal(1, Generator.GetParameters(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))], Generator.IdentifierName("t"))).Count);
        Assert.Equal(2, Generator.GetParameters(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("t")), Generator.ParameterDeclaration("p2", Generator.IdentifierName("t2"))], Generator.IdentifierName("t"))).Count);

        Assert.Equal(0, Generator.GetParameters(Generator.ValueReturningLambdaExpression(Generator.IdentifierName("expr"))).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.ValueReturningLambdaExpression("p1", Generator.IdentifierName("expr"))).Count);

        Assert.Equal(0, Generator.GetParameters(Generator.VoidReturningLambdaExpression(Generator.IdentifierName("expr"))).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.VoidReturningLambdaExpression("p1", Generator.IdentifierName("expr"))).Count);

        Assert.Equal(0, Generator.GetParameters(Generator.DelegateDeclaration("d")).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.DelegateDeclaration("d", parameters: [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);

        Assert.Equal(0, Generator.GetParameters(Generator.ClassDeclaration("c")).Count);
        Assert.Equal(0, Generator.GetParameters(Generator.IdentifierName("x")).Count);
    }

    [Fact]
    public void TestAddParameters()
    {
        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.MethodDeclaration("m"), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.ConstructorDeclaration(), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(3, Generator.GetParameters(Generator.AddParameters(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))], Generator.IdentifierName("t")), [Generator.ParameterDeclaration("p2", Generator.IdentifierName("t2")), Generator.ParameterDeclaration("p3", Generator.IdentifierName("t3"))])).Count);

        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.ValueReturningLambdaExpression(Generator.IdentifierName("expr")), [Generator.LambdaParameter("p")])).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.VoidReturningLambdaExpression(Generator.IdentifierName("expr")), [Generator.LambdaParameter("p")])).Count);

        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.DelegateDeclaration("d"), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);

        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.ClassDeclaration("c"), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.StructDeclaration("c"), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(1, Generator.GetParameters(Generator.AddParameters(Generator.InterfaceDeclaration("c"), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
        Assert.Equal(0, Generator.GetParameters(Generator.AddParameters(Generator.IdentifierName("x"), [Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))])).Count);
    }

    [Fact]
    public void TestGetExpression()
    {
        // initializers
        Assert.Equal("x", Generator.GetExpression(Generator.FieldDeclaration("f", Generator.IdentifierName("t"), initializer: Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.ParameterDeclaration("p", Generator.IdentifierName("t"), initializer: Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.LocalDeclarationStatement("loc", initializer: Generator.IdentifierName("x"))).ToString());

        // lambda bodies
        Assert.Null(Generator.GetExpression(Generator.ValueReturningLambdaExpression("p", [Generator.IdentifierName("x")])));
        Assert.Equal(1, Generator.GetStatements(Generator.ValueReturningLambdaExpression("p", [Generator.IdentifierName("x")])).Count);
        Assert.Equal("x", Generator.GetExpression(Generator.ValueReturningLambdaExpression(Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.VoidReturningLambdaExpression(Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.ValueReturningLambdaExpression("p", Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.VoidReturningLambdaExpression("p", Generator.IdentifierName("x"))).ToString());

        // identifier
        Assert.Null(Generator.GetExpression(Generator.IdentifierName("e")));

        // expression bodied methods
        var method = (MethodDeclarationSyntax)Generator.MethodDeclaration("p");
        method = method.WithBody(null).WithSemicolonToken(SemicolonToken);
        method = method.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.IdentifierName("x")));

        Assert.Equal("x", Generator.GetExpression(method).ToString());

        // expression bodied local functions
        var local = LocalFunctionStatement(PredefinedType(VoidKeyword), "p");
        local = local.WithBody(null).WithSemicolonToken(SemicolonToken);
        local = local.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.IdentifierName("x")));

        Assert.Equal("x", Generator.GetExpression(local).ToString());
    }

    [Fact]
    public void TestWithExpression()
    {
        // initializers
        Assert.Equal("x", Generator.GetExpression(Generator.WithExpression(Generator.FieldDeclaration("f", Generator.IdentifierName("t")), Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.WithExpression(Generator.ParameterDeclaration("p", Generator.IdentifierName("t")), Generator.IdentifierName("x"))).ToString());
        Assert.Equal("x", Generator.GetExpression(Generator.WithExpression(Generator.LocalDeclarationStatement(Generator.IdentifierName("t"), "loc"), Generator.IdentifierName("x"))).ToString());

        // lambda bodies
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.ValueReturningLambdaExpression("p", [Generator.IdentifierName("x")]), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.VoidReturningLambdaExpression("p", [Generator.IdentifierName("x")]), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.ValueReturningLambdaExpression([Generator.IdentifierName("x")]), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.VoidReturningLambdaExpression([Generator.IdentifierName("x")]), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.ValueReturningLambdaExpression("p", Generator.IdentifierName("x")), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.VoidReturningLambdaExpression("p", Generator.IdentifierName("x")), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.ValueReturningLambdaExpression(Generator.IdentifierName("x")), Generator.IdentifierName("y"))).ToString());
        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(Generator.VoidReturningLambdaExpression(Generator.IdentifierName("x")), Generator.IdentifierName("y"))).ToString());

        // identifier
        Assert.Null(Generator.GetExpression(Generator.WithExpression(Generator.IdentifierName("e"), Generator.IdentifierName("x"))));

        // expression bodied methods
        var method = (MethodDeclarationSyntax)Generator.MethodDeclaration("p");
        method = method.WithBody(null).WithSemicolonToken(SemicolonToken);
        method = method.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.IdentifierName("x")));

        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(method, Generator.IdentifierName("y"))).ToString());

        // expression bodied local functions
        var local = LocalFunctionStatement(PredefinedType(VoidKeyword), "p");
        local = local.WithBody(null).WithSemicolonToken(SemicolonToken);
        local = local.WithExpressionBody(ArrowExpressionClause((ExpressionSyntax)Generator.IdentifierName("x")));

        Assert.Equal("y", Generator.GetExpression(Generator.WithExpression(local, Generator.IdentifierName("y"))).ToString());
    }

    [Fact]
    public void TestAccessorDeclarations()
    {
        var prop = Generator.PropertyDeclaration("p", Generator.IdentifierName("T"));

        Assert.Equal(2, Generator.GetAccessors(prop).Count);

        // get accessors from property
        var getAccessor = Generator.GetAccessor(prop, DeclarationKind.GetAccessor);
        Assert.NotNull(getAccessor);
        VerifySyntax<AccessorDeclarationSyntax>(getAccessor,
@"get;");

        Assert.NotNull(getAccessor);
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(getAccessor));

        // get accessors from property
        var setAccessor = Generator.GetAccessor(prop, DeclarationKind.SetAccessor);
        Assert.NotNull(setAccessor);
        Assert.Equal(Accessibility.NotApplicable, Generator.GetAccessibility(setAccessor));

        // remove accessors
        Assert.Null(Generator.GetAccessor(Generator.RemoveNode(prop, getAccessor), DeclarationKind.GetAccessor));
        Assert.Null(Generator.GetAccessor(Generator.RemoveNode(prop, setAccessor), DeclarationKind.SetAccessor));

        // change accessor accessibility
        Assert.Equal(Accessibility.Public, Generator.GetAccessibility(Generator.WithAccessibility(getAccessor, Accessibility.Public)));
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(Generator.WithAccessibility(setAccessor, Accessibility.Private)));

        // change accessor statements
        Assert.Equal(0, Generator.GetStatements(getAccessor).Count);
        Assert.Equal(0, Generator.GetStatements(setAccessor).Count);

        var newGetAccessor = Generator.WithStatements(getAccessor, null);
        VerifySyntax<AccessorDeclarationSyntax>(newGetAccessor,
@"get;");

        var newNewGetAccessor = Generator.WithStatements(newGetAccessor, []);
        VerifySyntax<AccessorDeclarationSyntax>(newNewGetAccessor,
            """
            get
            {
            }
            """);

        // change accessors
        var newProp = Generator.ReplaceNode(prop, getAccessor, Generator.WithAccessibility(getAccessor, Accessibility.Public));
        Assert.Equal(Accessibility.Public, Generator.GetAccessibility(Generator.GetAccessor(newProp, DeclarationKind.GetAccessor)));

        newProp = Generator.ReplaceNode(prop, setAccessor, Generator.WithAccessibility(setAccessor, Accessibility.Public));
        Assert.Equal(Accessibility.Public, Generator.GetAccessibility(Generator.GetAccessor(newProp, DeclarationKind.SetAccessor)));
    }

    [Fact]
    public void TestAccessorDeclarations2()
    {
        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.WithAccessorDeclarations(Generator.PropertyDeclaration("p", Generator.IdentifierName("x"))),
            "x p { }");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.WithAccessorDeclarations(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("x")),
                Generator.GetAccessorDeclaration(Accessibility.NotApplicable, [Generator.ReturnStatement()])),
            "x p\r\n{\r\n    get\r\n    {\r\n        return;\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.WithAccessorDeclarations(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("x")),
                Generator.GetAccessorDeclaration(Accessibility.Protected, [Generator.ReturnStatement()])),
            "x p\r\n{\r\n    protected get\r\n    {\r\n        return;\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.WithAccessorDeclarations(
                Generator.PropertyDeclaration("p", Generator.IdentifierName("x")),
                Generator.SetAccessorDeclaration(Accessibility.Protected, [Generator.ReturnStatement()])),
            "x p\r\n{\r\n    protected set\r\n    {\r\n        return;\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.WithAccessorDeclarations(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))], Generator.IdentifierName("x"))),
            "x this[t p] { }");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.WithAccessorDeclarations(Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))], Generator.IdentifierName("x")),
                Generator.GetAccessorDeclaration(Accessibility.Protected, [Generator.ReturnStatement()])),
            "x this[t p]\r\n{\r\n    protected get\r\n    {\r\n        return;\r\n    }\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.WithAccessorDeclarations(
                Generator.IndexerDeclaration([Generator.ParameterDeclaration("p", Generator.IdentifierName("t"))], Generator.IdentifierName("x")),
                Generator.SetAccessorDeclaration(Accessibility.Protected, [Generator.ReturnStatement()])),
            "x this[t p]\r\n{\r\n    protected set\r\n    {\r\n        return;\r\n    }\r\n}");
    }

    [Fact]
    public void TestAccessorsOnSpecialProperties()
    {
        var root = ParseCompilationUnit(
            """
            class C
            {
               public int X { get; set; } = 100;
               public int Y => 300;
            }
            """);
        var x = Generator.GetMembers(root.Members[0])[0];
        var y = Generator.GetMembers(root.Members[0])[1];

        Assert.Equal(2, Generator.GetAccessors(x).Count);
        Assert.Equal(0, Generator.GetAccessors(y).Count);

        // adding accessors to expression value property will not succeed
        var y2 = Generator.AddAccessors(y, [Generator.GetAccessor(x, DeclarationKind.GetAccessor)]);
        Assert.NotNull(y2);
        Assert.Equal(0, Generator.GetAccessors(y2).Count);
    }

    [Fact]
    public void TestAccessorsOnSpecialIndexers()
    {
        var root = ParseCompilationUnit(
            """
            class C
            {
               public int this[int p] { get { return p * 10; } set { } };
               public int this[int p] => p * 10;
            }
            """);
        var x = Generator.GetMembers(root.Members[0])[0];
        var y = Generator.GetMembers(root.Members[0])[1];

        Assert.Equal(2, Generator.GetAccessors(x).Count);
        Assert.Equal(0, Generator.GetAccessors(y).Count);

        // adding accessors to expression value indexer will not succeed
        var y2 = Generator.AddAccessors(y, [Generator.GetAccessor(x, DeclarationKind.GetAccessor)]);
        Assert.NotNull(y2);
        Assert.Equal(0, Generator.GetAccessors(y2).Count);
    }

    [Fact]
    public void TestExpressionsOnSpecialProperties()
    {
        // you can get/set expression from both expression value property and initialized properties
        var root = ParseCompilationUnit(
            """
            class C
            {
               public int X { get; set; } = 100;
               public int Y => 300;
               public int Z { get; set; }
            }
            """);
        var x = Generator.GetMembers(root.Members[0])[0];
        var y = Generator.GetMembers(root.Members[0])[1];
        var z = Generator.GetMembers(root.Members[0])[2];

        Assert.NotNull(Generator.GetExpression(x));
        Assert.NotNull(Generator.GetExpression(y));
        Assert.Null(Generator.GetExpression(z));
        Assert.Equal("100", Generator.GetExpression(x).ToString());
        Assert.Equal("300", Generator.GetExpression(y).ToString());

        Assert.Equal("500", Generator.GetExpression(Generator.WithExpression(x, Generator.LiteralExpression(500))).ToString());
        Assert.Equal("500", Generator.GetExpression(Generator.WithExpression(y, Generator.LiteralExpression(500))).ToString());
        Assert.Equal("500", Generator.GetExpression(Generator.WithExpression(z, Generator.LiteralExpression(500))).ToString());
    }

    [Fact]
    public void TestExpressionsOnSpecialIndexers()
    {
        // you can get/set expression from both expression value property and initialized properties
        var root = ParseCompilationUnit(
            """
            class C
            {
               public int this[int p] { get { return p * 10; } set { } };
               public int this[int p] => p * 10;
            }
            """);
        var x = Generator.GetMembers(root.Members[0])[0];
        var y = Generator.GetMembers(root.Members[0])[1];

        Assert.Null(Generator.GetExpression(x));
        Assert.NotNull(Generator.GetExpression(y));
        Assert.Equal("p * 10", Generator.GetExpression(y).ToString());

        Assert.Null(Generator.GetExpression(Generator.WithExpression(x, Generator.LiteralExpression(500))));
        Assert.Equal("500", Generator.GetExpression(Generator.WithExpression(y, Generator.LiteralExpression(500))).ToString());
    }

    [Fact]
    public void TestGetStatements()
    {
        var stmts = new[]
        {
            // x = y;
            Generator.ExpressionStatement(Generator.AssignmentStatement(Generator.IdentifierName("x"), Generator.IdentifierName("y"))),

            // fn(arg);
            Generator.ExpressionStatement(Generator.InvocationExpression(Generator.IdentifierName("fn"), Generator.IdentifierName("arg")))
        };

        Assert.Equal(0, Generator.GetStatements(Generator.MethodDeclaration("m")).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.MethodDeclaration("m", statements: stmts)).Count);

        Assert.Equal(0, Generator.GetStatements(Generator.ConstructorDeclaration()).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.ConstructorDeclaration(statements: stmts)).Count);

        Assert.Equal(0, Generator.GetStatements(Generator.VoidReturningLambdaExpression([])).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.VoidReturningLambdaExpression(stmts)).Count);

        Assert.Equal(0, Generator.GetStatements(Generator.ValueReturningLambdaExpression([])).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.ValueReturningLambdaExpression(stmts)).Count);

        Assert.Equal(0, Generator.GetStatements(Generator.IdentifierName("x")).Count);
    }

    [Fact]
    public void TestWithStatements()
    {
        var stmts = new[]
        {
            // x = y;
            Generator.ExpressionStatement(Generator.AssignmentStatement(Generator.IdentifierName("x"), Generator.IdentifierName("y"))),

            // fn(arg);
            Generator.ExpressionStatement(Generator.InvocationExpression(Generator.IdentifierName("fn"), Generator.IdentifierName("arg")))
        };

        Assert.Equal(2, Generator.GetStatements(Generator.WithStatements(Generator.MethodDeclaration("m"), stmts)).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.WithStatements(Generator.ConstructorDeclaration(), stmts)).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.WithStatements(Generator.VoidReturningLambdaExpression([]), stmts)).Count);
        Assert.Equal(2, Generator.GetStatements(Generator.WithStatements(Generator.ValueReturningLambdaExpression([]), stmts)).Count);

        Assert.Equal(0, Generator.GetStatements(Generator.WithStatements(Generator.IdentifierName("x"), stmts)).Count);
    }

    [Fact]
    public void TestGetAccessorStatements()
    {
        var stmts = new[]
        {
            // x = y;
            Generator.ExpressionStatement(Generator.AssignmentStatement(Generator.IdentifierName("x"), Generator.IdentifierName("y"))),

            // fn(arg);
            Generator.ExpressionStatement(Generator.InvocationExpression(Generator.IdentifierName("fn"), Generator.IdentifierName("arg")))
        };

        var p = Generator.ParameterDeclaration("p", Generator.IdentifierName("t"));

        // get-accessor
        Assert.Equal(0, Generator.GetGetAccessorStatements(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"))).Count);
        Assert.Equal(2, Generator.GetGetAccessorStatements(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), getAccessorStatements: stmts)).Count);

        Assert.Equal(0, Generator.GetGetAccessorStatements(Generator.IndexerDeclaration([p], Generator.IdentifierName("t"))).Count);
        Assert.Equal(2, Generator.GetGetAccessorStatements(Generator.IndexerDeclaration([p], Generator.IdentifierName("t"), getAccessorStatements: stmts)).Count);

        Assert.Equal(0, Generator.GetGetAccessorStatements(Generator.IdentifierName("x")).Count);

        // set-accessor
        Assert.Equal(0, Generator.GetSetAccessorStatements(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"))).Count);
        Assert.Equal(2, Generator.GetSetAccessorStatements(Generator.PropertyDeclaration("p", Generator.IdentifierName("t"), setAccessorStatements: stmts)).Count);

        Assert.Equal(0, Generator.GetSetAccessorStatements(Generator.IndexerDeclaration([p], Generator.IdentifierName("t"))).Count);
        Assert.Equal(2, Generator.GetSetAccessorStatements(Generator.IndexerDeclaration([p], Generator.IdentifierName("t"), setAccessorStatements: stmts)).Count);

        Assert.Equal(0, Generator.GetSetAccessorStatements(Generator.IdentifierName("x")).Count);
    }

    [Fact]
    public void TestWithAccessorStatements()
    {
        var stmts = new[]
        {
            // x = y;
            Generator.ExpressionStatement(Generator.AssignmentStatement(Generator.IdentifierName("x"), Generator.IdentifierName("y"))),

            // fn(arg);
            Generator.ExpressionStatement(Generator.InvocationExpression(Generator.IdentifierName("fn"), Generator.IdentifierName("arg")))
        };

        var p = Generator.ParameterDeclaration("p", Generator.IdentifierName("t"));

        // get-accessor
        Assert.Equal(2, Generator.GetGetAccessorStatements(Generator.WithGetAccessorStatements(Generator.PropertyDeclaration("p", Generator.IdentifierName("t")), stmts)).Count);
        Assert.Equal(2, Generator.GetGetAccessorStatements(Generator.WithGetAccessorStatements(Generator.IndexerDeclaration([p], Generator.IdentifierName("t")), stmts)).Count);
        Assert.Equal(0, Generator.GetGetAccessorStatements(Generator.WithGetAccessorStatements(Generator.IdentifierName("x"), stmts)).Count);

        // set-accessor
        Assert.Equal(2, Generator.GetSetAccessorStatements(Generator.WithSetAccessorStatements(Generator.PropertyDeclaration("p", Generator.IdentifierName("t")), stmts)).Count);
        Assert.Equal(2, Generator.GetSetAccessorStatements(Generator.WithSetAccessorStatements(Generator.IndexerDeclaration([p], Generator.IdentifierName("t")), stmts)).Count);
        Assert.Equal(0, Generator.GetSetAccessorStatements(Generator.WithSetAccessorStatements(Generator.IdentifierName("x"), stmts)).Count);
    }

    [Fact]
    public void TestGetBaseAndInterfaceTypes()
    {
        var classBI = ParseCompilationUnit(
            """
            class C : B, I
            {
            }
            """).Members[0];

        var baseListBI = Generator.GetBaseAndInterfaceTypes(classBI);
        Assert.NotNull(baseListBI);
        Assert.Equal(2, baseListBI.Count);
        Assert.Equal("B", baseListBI[0].ToString());
        Assert.Equal("I", baseListBI[1].ToString());

        var classB = ParseCompilationUnit(
            """
            class C : B
            {
            }
            """).Members[0];

        var baseListB = Generator.GetBaseAndInterfaceTypes(classB);
        Assert.NotNull(baseListB);
        Assert.Equal(1, baseListB.Count);
        Assert.Equal("B", baseListB[0].ToString());

        var classN = ParseCompilationUnit(
            """
            class C
            {
            }
            """).Members[0];

        var baseListN = Generator.GetBaseAndInterfaceTypes(classN);
        Assert.NotNull(baseListN);
        Assert.Empty(baseListN);
    }

    [Fact]
    public void TestRemoveBaseAndInterfaceTypes()
    {
        var classBI = ParseCompilationUnit(
            """
            class C : B, I
            {
            }
            """).Members[0];

        var baseListBI = Generator.GetBaseAndInterfaceTypes(classBI);
        Assert.NotNull(baseListBI);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNode(classBI, baseListBI[0]),
            """
            class C : I
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNode(classBI, baseListBI[1]),
            """
            class C : B
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(classBI, baseListBI),
            """
            class C
            {
            }
            """);
    }

    [Fact]
    public void TestAddBaseType()
    {
        var classC = ParseCompilationUnit(
            """
            class C
            {
            }
            """).Members[0];

        var classCI = ParseCompilationUnit(
            """
            class C : I
            {
            }
            """).Members[0];

        var classCB = ParseCompilationUnit(
            """
            class C : B
            {
            }
            """).Members[0];

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddBaseType(classC, Generator.IdentifierName("T")),
            """
            class C : T
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddBaseType(classCI, Generator.IdentifierName("T")),
            """
            class C : T, I
            {
            }
            """);

        // TODO: find way to avoid this
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddBaseType(classCB, Generator.IdentifierName("T")),
            """
            class C : T, B
            {
            }
            """);
    }

    [Fact]
    public void TestAddInterfaceTypes()
    {
        var classC = ParseCompilationUnit(
            """
            class C
            {
            }
            """).Members[0];

        var classCI = ParseCompilationUnit(
            """
            class C : I
            {
            }
            """).Members[0];

        var classCB = ParseCompilationUnit(
            """
            class C : B
            {
            }
            """).Members[0];

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddInterfaceType(classC, Generator.IdentifierName("T")),
            """
            class C : T
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddInterfaceType(classCI, Generator.IdentifierName("T")),
            """
            class C : I, T
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.AddInterfaceType(classCB, Generator.IdentifierName("T")),
            """
            class C : B, T
            {
            }
            """);
    }

    [Fact]
    public void TestMultiFieldDeclarations()
    {
        var comp = Compile(
            """
            public class C
            {
               public static int X, Y, Z;
            }
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var symbolX = (IFieldSymbol)symbolC.GetMembers("X").First();
        var symbolY = (IFieldSymbol)symbolC.GetMembers("Y").First();
        var symbolZ = (IFieldSymbol)symbolC.GetMembers("Z").First();

        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());
        var declX = Generator.GetDeclaration(symbolX.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());
        var declY = Generator.GetDeclaration(symbolY.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());
        var declZ = Generator.GetDeclaration(symbolZ.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(declX));
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(declY));
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(declZ));

        Assert.NotNull(Generator.GetType(declX));
        Assert.Equal("int", Generator.GetType(declX).ToString());
        Assert.Equal("X", Generator.GetName(declX));
        Assert.Equal(Accessibility.Public, Generator.GetAccessibility(declX));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(declX));

        Assert.NotNull(Generator.GetType(declY));
        Assert.Equal("int", Generator.GetType(declY).ToString());
        Assert.Equal("Y", Generator.GetName(declY));
        Assert.Equal(Accessibility.Public, Generator.GetAccessibility(declY));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(declY));

        Assert.NotNull(Generator.GetType(declZ));
        Assert.Equal("int", Generator.GetType(declZ).ToString());
        Assert.Equal("Z", Generator.GetName(declZ));
        Assert.Equal(Accessibility.Public, Generator.GetAccessibility(declZ));
        Assert.Equal(DeclarationModifiers.Static, Generator.GetModifiers(declZ));

        var xTypedT = Generator.WithType(declX, Generator.IdentifierName("T"));
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(xTypedT));
        Assert.Equal(SyntaxKind.FieldDeclaration, xTypedT.Kind());
        Assert.Equal("T", Generator.GetType(xTypedT).ToString());

        var xNamedQ = Generator.WithName(declX, "Q");
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(xNamedQ));
        Assert.Equal(SyntaxKind.FieldDeclaration, xNamedQ.Kind());
        Assert.Equal("Q", Generator.GetName(xNamedQ).ToString());

        var xInitialized = Generator.WithExpression(declX, Generator.IdentifierName("e"));
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(xInitialized));
        Assert.Equal(SyntaxKind.FieldDeclaration, xInitialized.Kind());
        Assert.Equal("e", Generator.GetExpression(xInitialized).ToString());

        var xPrivate = Generator.WithAccessibility(declX, Accessibility.Private);
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(xPrivate));
        Assert.Equal(SyntaxKind.FieldDeclaration, xPrivate.Kind());
        Assert.Equal(Accessibility.Private, Generator.GetAccessibility(xPrivate));

        var xReadOnly = Generator.WithModifiers(declX, DeclarationModifiers.ReadOnly);
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(xReadOnly));
        Assert.Equal(SyntaxKind.FieldDeclaration, xReadOnly.Kind());
        Assert.Equal(DeclarationModifiers.ReadOnly, Generator.GetModifiers(xReadOnly));

        var xAttributed = Generator.AddAttributes(declX, Generator.Attribute("A"));
        Assert.Equal(DeclarationKind.Field, Generator.GetDeclarationKind(xAttributed));
        Assert.Equal(SyntaxKind.FieldDeclaration, xAttributed.Kind());
        Assert.Equal(1, Generator.GetAttributes(xAttributed).Count);
        Assert.Equal("[A]", Generator.GetAttributes(xAttributed)[0].ToString());

        var membersC = Generator.GetMembers(declC);
        Assert.Equal(3, membersC.Count);
        Assert.Equal(declX, membersC[0]);
        Assert.Equal(declY, membersC[1]);
        Assert.Equal(declZ, membersC[2]);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public class C
            {
                T A;
                public static int X, Y, Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertMembers(declC, 1, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public class C
            {
                public static int X;
                T A;
                public static int Y, Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertMembers(declC, 2, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public class C
            {
                public static int X, Y;
                T A;
                public static int Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertMembers(declC, 3, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public class C
            {
                public static int X, Y, Z;
                T A;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ClassDeclaration("C", members: [declX, declY]),
            """
            class C
            {
                public static int X;
                public static int Y;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declX, xTypedT),
            """
            public class C
            {
                public static T X;
                public static int Y, Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declY, Generator.WithType(declY, Generator.IdentifierName("T"))),
            """
            public class C
            {
                public static int X;
                public static T Y;
                public static int Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declZ, Generator.WithType(declZ, Generator.IdentifierName("T"))),
            """
            public class C
            {
                public static int X, Y;
                public static T Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declX, Generator.WithAccessibility(declX, Accessibility.Private)),
            """
            public class C
            {
                private static int X;
                public static int Y, Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declX, Generator.WithModifiers(declX, DeclarationModifiers.None)),
            """
            public class C
            {
                public int X;
                public static int Y, Z;
            }
            """);
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declX, Generator.WithName(declX, "Q")),
            """
            public class C
            {
                public static int Q, Y, Z;
            }
            """);
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declX.GetAncestorOrThis<VariableDeclaratorSyntax>(), VariableDeclarator("Q")),
            """
            public class C
            {
                public static int Q, Y, Z;
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, declX, Generator.WithExpression(declX, Generator.IdentifierName("e"))),
            """
            public class C
            {
                public static int X = e, Y, Z;
            }
            """);
    }

    [Theory, WorkItem("https://github.com/dotnet/roslyn/issues/48789")]
    [InlineData("record")]
    [InlineData("record class")]
    public void TestInsertMembersOnRecord_SemiColon(string typeKind)
    {
        var comp = Compile(
            $"""
            public {typeKind} C;
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            $$"""
            public {{typeKind}} C
            {
                T A;
            }
            """);
    }

    [Fact]
    public void TestInsertMembersOnClass_SemiColon()
    {
        var comp = Compile(
            $"""
            public class C;
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            $$"""
            public class C
            {
                T A;
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/48789")]
    public void TestInsertMembersOnRecordStruct_SemiColon()
    {
        var src =
            """
            public record struct C;
            """;
        var comp = CSharpCompilation.Create("test")
            .AddReferences(NetFramework.mscorlib)
            .AddSyntaxTrees(ParseSyntaxTree(src, options: CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview)));

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public record struct C
            {
                T A;
            }
            """);
    }

    [Fact]
    public void TestInsertMembersOnStruct_SemiColon()
    {
        var comp = Compile(
            $"""
            public struct C;
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<StructDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            $$"""
            public struct C
            {
                T A;
            }
            """);
    }

    [Fact]
    public void TestInsertMembersOnInterface_SemiColon_01()
    {
        var comp = Compile(
            $"""
            public interface C;
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.PropertyDeclaration("A", Generator.IdentifierName("T"))),
            $$"""
            public interface C
            {
                T A { get; set; }
            }
            """);
    }

    [Fact]
    public void TestInsertMembersOnInterface_SemiColon_02()
    {
        var comp = Compile(
            $"""
            public interface C();
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<InterfaceDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.PropertyDeclaration("A", Generator.IdentifierName("T"))),
            $$"""
            public interface C()
            {
                T A { get; set; }
            }
            """);
    }

    [Fact]
    public void TestInsertMembersOnEnum_SemiColon()
    {
        var comp = Compile(
            $"""
            public enum C;
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<EnumDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.EnumMember("A")),
            $$"""
            public enum C
            {
                A
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/48789")]
    public void TestInsertMembersOnRecord_Braces()
    {
        var comp = Compile(
            """
            public record C { }
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public record C
            {
                T A;
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/48789")]
    public void TestInsertMembersOnRecord_BracesAndSemiColon()
    {
        var comp = Compile(
            """
            public record C { };
            """);

        var symbolC = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("C").First();
        var declC = Generator.GetDeclaration(symbolC.DeclaringSyntaxReferences.Select(x => x.GetSyntax()).First());

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.InsertMembers(declC, 0, Generator.FieldDeclaration("A", Generator.IdentifierName("T"))),
            """
            public record C
            {
                T A;
            }
            """);
    }

    [Fact]
    public void TestMultiAttributeDeclarations()
    {
        var comp = Compile(
            """
            [X, Y, Z]
            public class C
            {
            }
            """);
        var symbolC = comp.GlobalNamespace.GetMembers("C").First();
        var declC = symbolC.DeclaringSyntaxReferences.First().GetSyntax();
        var attrs = Generator.GetAttributes(declC);

        var attrX = attrs[0];
        var attrY = attrs[1];
        var attrZ = attrs[2];

        Assert.Equal(3, attrs.Count);
        Assert.Equal("X", Generator.GetName(attrX));
        Assert.Equal("Y", Generator.GetName(attrY));
        Assert.Equal("Z", Generator.GetName(attrZ));

        var xNamedQ = Generator.WithName(attrX, "Q");
        Assert.Equal(DeclarationKind.Attribute, Generator.GetDeclarationKind(xNamedQ));
        Assert.Equal(SyntaxKind.AttributeList, xNamedQ.Kind());
        Assert.Equal("[Q]", xNamedQ.ToString());

        var xWithArg = Generator.AddAttributeArguments(attrX, [Generator.AttributeArgument(Generator.IdentifierName("e"))]);
        Assert.Equal(DeclarationKind.Attribute, Generator.GetDeclarationKind(xWithArg));
        Assert.Equal(SyntaxKind.AttributeList, xWithArg.Kind());
        Assert.Equal("[X(e)]", xWithArg.ToString());

        // Inserting new attributes
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertAttributes(declC, 0, Generator.Attribute("A")),
            """
            [A]
            [X, Y, Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertAttributes(declC, 1, Generator.Attribute("A")),
            """
            [X]
            [A]
            [Y, Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertAttributes(declC, 2, Generator.Attribute("A")),
            """
            [X, Y]
            [A]
            [Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.InsertAttributes(declC, 3, Generator.Attribute("A")),
            """
            [X, Y, Z]
            [A]
            public class C
            {
            }
            """);

        // Removing attributes
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrX]),
            """
            [Y, Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrY]),
            """
            [X, Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrZ]),
            """
            [X, Y]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrX, attrY]),
            """
            [Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrX, attrZ]),
            """
            [Y]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrY, attrZ]),
            """
            [X]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.RemoveNodes(declC, [attrX, attrY, attrZ]),
            """
            public class C
            {
            }
            """);

        // Replacing attributes
        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, attrX, Generator.Attribute("A")),
            """
            [A, Y, Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, attrY, Generator.Attribute("A")),
            """
            [X, A, Z]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, attrZ, Generator.Attribute("A")),
            """
            [X, Y, A]
            public class C
            {
            }
            """);

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.ReplaceNode(declC, attrX, Generator.AddAttributeArguments(attrX, [Generator.AttributeArgument(Generator.IdentifierName("e"))])),
            """
            [X(e), Y, Z]
            public class C
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66376")]
    public void TestRefReturnType()
    {
        var comp = Compile(
            """
            public class C<T>
            {
                public ref T GetPinnableReference() { throw null; }
                public ref readonly T this[int index] { get { throw null; } }
                public ref int P => throw null;
            }
            """);
        var symbolC = comp.GlobalNamespace.GetMembers("C").First();

        var method = symbolC.GetMembers().OfType<IMethodSymbol>().Single(m => m.MethodKind == MethodKind.Ordinary);
        var indexer = symbolC.GetMembers().OfType<IPropertySymbol>().Single(m => m.IsIndexer);
        var property = symbolC.GetMembers().OfType<IPropertySymbol>().Single(m => !m.IsIndexer);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.MethodDeclaration(method),
            "public ref T GetPinnableReference()\r\n{\r\n}");

        VerifySyntax<IndexerDeclarationSyntax>(
            Generator.IndexerDeclaration(indexer),
            "public ref readonly T this[global::System.Int32 index]\r\n{\r\n    get\r\n    {\r\n    }\r\n}");

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.PropertyDeclaration(property),
            "public ref global::System.Int32 P { get; }");
    }

    [Fact]
    public void TestMultiReturnAttributeDeclarations()
    {
        var comp = Compile(
            """
            public class C
            {
                [return: X, Y, Z]
                public void M()
                {
                }
            }
            """);
        var symbolC = comp.GlobalNamespace.GetMembers("C").First();
        var declC = symbolC.DeclaringSyntaxReferences.First().GetSyntax();
        var declM = Generator.GetMembers(declC).First();

        Assert.Equal(0, Generator.GetAttributes(declM).Count);

        var attrs = Generator.GetReturnAttributes(declM);
        Assert.Equal(3, attrs.Count);
        var attrX = attrs[0];
        var attrY = attrs[1];
        var attrZ = attrs[2];

        Assert.Equal("X", Generator.GetName(attrX));
        Assert.Equal("Y", Generator.GetName(attrY));
        Assert.Equal("Z", Generator.GetName(attrZ));

        var xNamedQ = Generator.WithName(attrX, "Q");
        Assert.Equal(DeclarationKind.Attribute, Generator.GetDeclarationKind(xNamedQ));
        Assert.Equal(SyntaxKind.AttributeList, xNamedQ.Kind());
        Assert.Equal("[Q]", xNamedQ.ToString());

        var xWithArg = Generator.AddAttributeArguments(attrX, [Generator.AttributeArgument(Generator.IdentifierName("e"))]);
        Assert.Equal(DeclarationKind.Attribute, Generator.GetDeclarationKind(xWithArg));
        Assert.Equal(SyntaxKind.AttributeList, xWithArg.Kind());
        Assert.Equal("[X(e)]", xWithArg.ToString());

        // Inserting new attributes
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 0, Generator.Attribute("A")),
            """
            [return: A]
            [return: X, Y, Z]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 1, Generator.Attribute("A")),
            """
            [return: X]
            [return: A]
            [return: Y, Z]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 2, Generator.Attribute("A")),
            """
            [return: X, Y]
            [return: A]
            [return: Z]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 3, Generator.Attribute("A")),
            """
            [return: X, Y, Z]
            [return: A]
            public void M()
            {
            }
            """);

        // replacing
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.ReplaceNode(declM, attrX, Generator.Attribute("Q")),
            """
            [return: Q, Y, Z]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.ReplaceNode(declM, attrX, Generator.AddAttributeArguments(attrX, [Generator.AttributeArgument(Generator.IdentifierName("e"))])),
            """
            [return: X(e), Y, Z]
            public void M()
            {
            }
            """);
    }

    [Fact]
    public void TestMixedAttributeDeclarations()
    {
        var comp = Compile(
            """
            public class C
            {
                [X]
                [return: A]
                [Y, Z]
                [return: B, C, D]
                [P]
                public void M()
                {
                }
            }
            """);
        var symbolC = comp.GlobalNamespace.GetMembers("C").First();
        var declC = symbolC.DeclaringSyntaxReferences.First().GetSyntax();
        var declM = Generator.GetMembers(declC).First();

        var attrs = Generator.GetAttributes(declM);
        Assert.Equal(4, attrs.Count);

        var attrX = attrs[0];
        Assert.Equal("X", Generator.GetName(attrX));
        Assert.Equal(SyntaxKind.AttributeList, attrX.Kind());

        var attrY = attrs[1];
        Assert.Equal("Y", Generator.GetName(attrY));
        Assert.Equal(SyntaxKind.Attribute, attrY.Kind());

        var attrZ = attrs[2];
        Assert.Equal("Z", Generator.GetName(attrZ));
        Assert.Equal(SyntaxKind.Attribute, attrZ.Kind());

        var attrP = attrs[3];
        Assert.Equal("P", Generator.GetName(attrP));
        Assert.Equal(SyntaxKind.AttributeList, attrP.Kind());

        var rattrs = Generator.GetReturnAttributes(declM);
        Assert.Equal(4, rattrs.Count);

        var attrA = rattrs[0];
        Assert.Equal("A", Generator.GetName(attrA));
        Assert.Equal(SyntaxKind.AttributeList, attrA.Kind());

        var attrB = rattrs[1];
        Assert.Equal("B", Generator.GetName(attrB));
        Assert.Equal(SyntaxKind.Attribute, attrB.Kind());

        var attrC = rattrs[2];
        Assert.Equal("C", Generator.GetName(attrC));
        Assert.Equal(SyntaxKind.Attribute, attrC.Kind());

        var attrD = rattrs[3];
        Assert.Equal("D", Generator.GetName(attrD));
        Assert.Equal(SyntaxKind.Attribute, attrD.Kind());

        // inserting
        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertAttributes(declM, 0, Generator.Attribute("Q")),
            """
            [Q]
            [X]
            [return: A]
            [Y, Z]
            [return: B, C, D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertAttributes(declM, 1, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Q]
            [Y, Z]
            [return: B, C, D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertAttributes(declM, 2, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y]
            [Q]
            [Z]
            [return: B, C, D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertAttributes(declM, 3, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y, Z]
            [return: B, C, D]
            [Q]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertAttributes(declM, 4, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y, Z]
            [return: B, C, D]
            [P]
            [Q]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 0, Generator.Attribute("Q")),
            """
            [X]
            [return: Q]
            [return: A]
            [Y, Z]
            [return: B, C, D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 1, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y, Z]
            [return: Q]
            [return: B, C, D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 2, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y, Z]
            [return: B]
            [return: Q]
            [return: C, D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 3, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y, Z]
            [return: B, C]
            [return: Q]
            [return: D]
            [P]
            public void M()
            {
            }
            """);

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.InsertReturnAttributes(declM, 4, Generator.Attribute("Q")),
            """
            [X]
            [return: A]
            [Y, Z]
            [return: B, C, D]
            [return: Q]
            [P]
            public void M()
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/293")]
    [Trait(Traits.Feature, Traits.Features.Formatting)]
    public void IntroduceBaseList()
    {
        var text = """
            public class C
            {
            }
            """;
        var root = ParseCompilationUnit(text);
        var decl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        var newDecl = Generator.AddInterfaceType(decl, Generator.IdentifierName("IDisposable"));
        var newRoot = root.ReplaceNode(decl, newDecl);

        var elasticOnlyFormatted = Formatter.Format(newRoot, SyntaxAnnotation.ElasticAnnotation, _workspace.Services.SolutionServices, CSharpSyntaxFormattingOptions.Default, CancellationToken.None).ToFullString();
        Assert.Equal("""
            public class C : IDisposable
            {
            }
            """, elasticOnlyFormatted);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/67335")]
    public void TestGenerateRecordClass1()
    {
        var comp = Compile(
@"public record class R;");

        var symbolR = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("R").First();

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.Declaration(symbolR),
            """
            public record R : global::System.Object, global::System.IEquatable<global::R>;
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/67335")]
    public void TestGenerateRecordClass2()
    {
        var comp = Compile(
@"public record class R(int i) { public int I => i; }");

        var symbolR = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("R").First();

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.Declaration(symbolR),
            """
            public record R : global::System.Object, global::System.IEquatable<global::R>
            {
                public R(global::System.Int32 i)
                {
                }

                public global::System.Int32 i { get; init; }
                public global::System.Int32 I { get; }
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/67335")]
    public void TestGenerateRecordStruct1()
    {
        var comp = Compile(
@"public record struct R;");

        var symbolR = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("R").First();

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.Declaration(symbolR),
            """
            public record struct R : global::System.IEquatable<global::R>;
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/67335")]
    public void TestGenerateRecordStruct2()
    {
        var comp = Compile(
@"public readonly record struct R(int i) { public int I => i; }");

        var symbolR = (INamedTypeSymbol)comp.GlobalNamespace.GetMembers("R").First();

        VerifySyntax<RecordDeclarationSyntax>(
            Generator.Declaration(symbolR),
            """
            public readonly record struct R : global::System.IEquatable<global::R>
            {
                public R(global::System.Int32 i)
                {
                }

                public global::System.Int32 i { get; init; }
                public global::System.Int32 I { get; }
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68957")]
    public void TestGenerateUnmanagedConstraint1()
    {
        var comp = Compile(
            """public class C<T> where T : unmanaged { }""");

        var symbol = comp.GlobalNamespace.GetMembers("C").First();

        VerifySyntax<ClassDeclarationSyntax>(
            Generator.Declaration(symbol),
            """
            public class C<T> : global::System.Object where T : unmanaged
            {
                public C()
                {
                }
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68957")]
    public void TestGenerateUnmanagedConstraint2()
    {
        var comp = Compile(
            """public class C { void M<T>() where T : unmanaged { } }""");

        var symbol = comp.GlobalNamespace.GetMembers("C").First().GetMembers("M").First();

        VerifySyntax<MethodDeclarationSyntax>(
            Generator.Declaration(symbol),
            """
            private void M<T>()
                where T : unmanaged
            {
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/68957")]
    public void TestGenerateInitProperty1()
    {
        var comp = Compile(
            """public class C { public int X { get; init; } }""");

        var symbol = comp.GlobalNamespace.GetMembers("C").First().GetMembers("X").First();

        VerifySyntax<PropertyDeclarationSyntax>(
            Generator.Declaration(symbol),
            """
            public global::System.Int32 X { get; init; }
            """);
    }

    [Theory, WorkItem("https://github.com/dotnet/roslyn/issues/66966")]
    [InlineData("abstract")]
    [InlineData("virtual")]
    public void TestGenerateAbstractOrVirtualEvent(string modifier)
    {
        var comp = Compile(
            $$"""public abstract class C { public {{modifier}} event System.EventHandler MyEvent; }""");

        var symbol = comp.GlobalNamespace.GetMembers("C").First().GetMembers("MyEvent").First();

        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.Declaration(symbol),
            $$"""
            public {{modifier}} event global::System.EventHandler MyEvent;
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66966")]
    public void TestGenerateSealedOrOverrideEvent()
    {
        var comp = Compile(
            """
            public abstract class C
            {
                public abstract event System.EventHandler MyEvent;
            }

            public class C2 : C
            {
                public sealed override event System.EventHandler MyEvent;
            }
            """);

        var symbol = comp.GlobalNamespace.GetMembers("C2").First().GetMembers("MyEvent").First();

        VerifySyntax<EventFieldDeclarationSyntax>(
            Generator.Declaration(symbol),
            """
            public sealed override event global::System.EventHandler MyEvent;
            """);
    }

    #endregion

    #region DeclarationModifiers

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestNamespaceModifiers()
    {
        TestModifiersAsync(DeclarationModifiers.None,
            """
            [|namespace N1
            {
            }|]
            """);
    }

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestFileScopedNamespaceModifiers()
    {
        TestModifiersAsync(DeclarationModifiers.None,
            """
            [|namespace N1;|]
            """);
    }

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestClassModifiers1()
    {
        TestModifiersAsync(DeclarationModifiers.Static,
            """
            [|static class C
            {
            }|]
            """);
    }

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestMethodModifiers()
    {
        TestModifiersAsync(DeclarationModifiers.Sealed | DeclarationModifiers.Override,
            """
            class C
            {
                [|public sealed override void M() { }|]
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/66170")]
    public void TestMethodModifiers2()
    {
        TestModifiersAsync(DeclarationModifiers.ReadOnly,
            """
            struct S
            {
                [|public readonly void M() { }|]
            }
            """);
    }

    [Fact]
    public void TestAsyncMethodModifier()
    {
        TestModifiersAsync(DeclarationModifiers.Async,
            """
            using System.Threading.Tasks;
            class C
            {
                [|public async Task DoAsync() { await Task.CompletedTask; }|]
            }
            """);
    }

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestPropertyModifiers1()
    {
        TestModifiersAsync(DeclarationModifiers.Virtual | DeclarationModifiers.ReadOnly,
            """
            class C
            {
                [|public virtual int X => 0;|]
            }
            """);
    }

    [Fact]
    public void TestPropertyModifiers2()
    {
        TestModifiersAsync(DeclarationModifiers.ReadOnly | DeclarationModifiers.Required,
            """
            class C
            {
                [|public required int X => 0;|]
            }
            """);
    }

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestFieldModifiers1()
    {
        TestModifiersAsync(DeclarationModifiers.Static,
            """
            class C
            {
                public static int [|X|];
            }
            """);
    }

    [Fact]
    public void TestFieldModifiers2()
    {
        TestModifiersAsync(DeclarationModifiers.Required,
            """
            class C
            {
                public required int [|X|];
            }
            """);
    }

    [Fact, WorkItem(" https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1084965")]
    public void TestEvent1()
    {
        TestModifiersAsync(DeclarationModifiers.Virtual,
            """
            class C
            {
                public virtual event System.Action [|X|];
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/65834")]
    public void TestStructModifiers1()
    {
        TestModifiersAsync(DeclarationModifiers.ReadOnly | DeclarationModifiers.Sealed,
            """
            public readonly struct [|S|]
            {
            }
            """);
    }

    [Fact]
    public void TestStructModifiers2()
    {
        var compilation = Compile("""
            public ref struct S
            {
                public int Value;
                public ref int RefValue;
                public readonly ref int RORefValue;

                public void M(scoped ref int value) { }
                public void M(scoped ref readonly double value) { } 
                public void M(in int x, scoped in int y) { }
                public void M(S x, scoped S y) { }
                public void M(out int value) { }
            }
            """);

        var symbol = compilation.GlobalNamespace.GetMembers("S").Single();
        VerifySyntax<StructDeclarationSyntax>(
            Generator.Declaration(symbol),
            """
            public ref struct S
            {
                public global::System.Int32 Value;
                public ref global::System.Int32 RefValue;
                public readonly ref global::System.Int32 RORefValue;
                public void M(scoped ref global::System.Int32 value)
                {
                }

                public void M(scoped ref readonly global::System.Double value)
                {
                }

                public void M(in global::System.Int32 x, scoped in global::System.Int32 y)
                {
                }

                public void M(global::S x, scoped global::S y)
                {
                }

                public void M(out global::System.Int32 value)
                {
                }

                public S()
                {
                }
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/67341")]
    public void TestUnboundGenerics()
    {
        var compilation = Compile("""
            using System;

            [AttributeUsage(AttributeTargets.All)]
            public class TypeAttribute : Attribute
            {
                public TypeAttribute(params Type[] types)
                {
                }
            }

            [TypeAttribute(typeof((int, string)), typeof((int x, string y)), typeof(ValueTuple<,>), typeof(ValueTuple<int, string>))]
            public class C
            {
            }
            """);

        var symbol = compilation.GlobalNamespace.GetMembers("C").Single();
        var attribute = symbol.GetAttributes().Single();

        VerifySyntax<AttributeListSyntax>(
            Generator.Attribute(attribute),
            """
            [global::TypeAttribute(new[] { typeof((global::System.Int32, global::System.String)), typeof((global::System.Int32 x, global::System.String y)), typeof(global::System.ValueTuple<, >), typeof((global::System.Int32, global::System.String)) })]
            """);

        var type = (ITypeSymbol)attribute.ConstructorArguments[0].Values[2].Value;

        VerifySyntax<TypeSyntax>(
            Generator.TypeExpression(type),
            """
            global::System.ValueTuple<, >
            """);
    }

    private static void TestModifiersAsync(DeclarationModifiers modifiers, string markup)
    {
        MarkupTestFile.GetSpan(markup, out var code, out var span);

        var compilation = Compile(code);
        var tree = compilation.SyntaxTrees.Single();

        var semanticModel = compilation.GetSemanticModel(tree);

        var root = tree.GetRoot();
        var node = root.FindNode(span, getInnermostNodeForTie: true);

        var declaration = semanticModel.GetDeclaredSymbol(node);
        Assert.NotNull(declaration);

        Assert.Equal(modifiers, DeclarationModifiers.From(declaration));
    }

    #endregion
}
