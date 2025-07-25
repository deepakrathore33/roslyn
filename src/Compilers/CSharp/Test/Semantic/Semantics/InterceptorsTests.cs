﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma warning disable RSEXPERIMENTAL002 // Tests for experimental API

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Test.Utilities;
using Roslyn.Test.Utilities;
using Roslyn.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests.Semantics;

public class InterceptorsTests : CSharpTestBase
{
    private static readonly (string text, string path) s_attributesSource = ("""
        namespace System.Runtime.CompilerServices;

        [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
        public sealed class InterceptsLocationAttribute : Attribute
        {
            public InterceptsLocationAttribute(string filePath, int line, int character) { }
            public InterceptsLocationAttribute(int version, string data) { }
        }
        """, "attributes.cs");

    private static readonly CSharpParseOptions RegularWithInterceptors = TestOptions.Regular.WithFeature("InterceptorsNamespaces", "global");
    private static readonly CSharpParseOptions RegularPreviewWithInterceptors = TestOptions.RegularPreview.WithFeature("InterceptorsNamespaces", "global");

    private static readonly SyntaxTree s_attributesTree = CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors);

    private static ImmutableArray<InterceptableLocation?> GetInterceptableLocations(CSharpTestSource source)
    {
        var comp = CreateCompilation(source);
        var tree = comp.SyntaxTrees.Single();
        var model = comp.GetSemanticModel(tree);

        var nodes = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().SelectAsArray(node => model.GetInterceptableLocation(node));
        return nodes;
    }

    private static string GetAttributeArgs(InterceptableLocation location) => $@"{location.Version}, ""{location.Data}""";

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/76641")]
    public void UnsupportedWarningWave()
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """, "Program.cs");
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;
            class D
            {
                [InterceptsLocation("Program.cs", 1, 3)]
                public static void M() => Console.Write(1);
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, options: TestOptions.DebugExe.WithWarningLevel(8));
        comp.VerifyEmitDiagnostics();

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, options: TestOptions.DebugExe.WithWarningLevel(9));
        comp.VerifyEmitDiagnostics(
            // (5,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 1, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 1, 3)").WithLocation(5, 6));

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 1, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 1, 3)").WithLocation(5, 6));
    }

    [Fact]
    public void FeatureFlag()
    {
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;
            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M() => Console.Write(1);
            }
            """;

        var sadCaseDiagnostics = new[]
        {
            // (5,6): error CS9206: An interceptor cannot be declared in the global namespace.
            //     [InterceptsLocation(1, "eY+urAo7Kg2rsKgGSGjShwIAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorGlobalNamespace, "InterceptsLocation").WithLocation(5, 6)
        };
        var comp = CreateCompilation([source, interceptors, s_attributesSource]);
        comp.VerifyEmitDiagnostics(sadCaseDiagnostics);

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsPreview-experimental"));
        comp.VerifyEmitDiagnostics(sadCaseDiagnostics);

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsPreview", "false"));
        comp.VerifyEmitDiagnostics(sadCaseDiagnostics);

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("interceptorspreview"));
        comp.VerifyEmitDiagnostics(sadCaseDiagnostics);

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsPreview", "Global"));
        comp.VerifyEmitDiagnostics(sadCaseDiagnostics);

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsPreview", "global.a"));
        comp.VerifyEmitDiagnostics(sadCaseDiagnostics);

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void FeatureFlag_Granular_01()
    {
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            namespace NS1
            {
                class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void M() => Console.Write(1);
                }
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS"));
        comp.VerifyEmitDiagnostics(
            // (8,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>' to your project.
            //         [InterceptsLocation(1, "eY+urAo7Kg2rsKgGSGjShwIAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>").WithLocation(8, 10));

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS1.NS2"));
        comp.VerifyEmitDiagnostics(
            // (8,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>' to your project.
            //         [InterceptsLocation(1, "eY+urAo7Kg2rsKgGSGjShwIAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>").WithLocation(8, 10));

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS1"), expectedOutput: "1");
        verifier.VerifyDiagnostics();

        verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS1;NS2"), expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void FeatureFlag_Granular_Checksum_01()
    {
        test(TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS"), expectedOutput: null,
            // Interceptors.cs(7,10): error CS9137: The 'interceptors' experimental feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>' to your project.
            //         [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "eY+urAo7Kg2rsKgGSGjShwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "global::System.Runtime.CompilerServices.InterceptsLocationAttribute").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>").WithLocation(7, 10));

        test(TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS1.NS2"), expectedOutput: null,
            // Interceptors.cs(7,10): error CS9137: The 'interceptors' experimental feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>' to your project.
            //         [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "eY+urAo7Kg2rsKgGSGjShwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "global::System.Runtime.CompilerServices.InterceptsLocationAttribute").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1</InterceptorsNamespaces>").WithLocation(7, 10));

        test(TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS1"), expectedOutput: "1");

        test(TestOptions.Regular.WithFeature("InterceptorsNamespaces", "NS1;NS2"), expectedOutput: "1");

        void test(CSharpParseOptions options, string? expectedOutput, params DiagnosticDescription[] expected)
        {
            var source = CSharpTestSource.Parse("""
                C.M();

                class C
                {
                    public static void M() => throw null!;
                }
                """, path: "Program.cs", options);

            var comp = CreateCompilation(source);
            comp.VerifyEmitDiagnostics();

            var model = comp.GetSemanticModel(source);
            var invocation = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
            var interceptableLocation = model.GetInterceptableLocation(invocation)!;

            var interceptors = CSharpTestSource.Parse($$"""
                using System;

                namespace NS1
                {
                    class D
                    {
                        {{interceptableLocation.GetInterceptsLocationAttributeSyntax()}}
                        public static void M() => Console.Write(1);
                    }
                }
                """, path: "Interceptors.cs", options);
            var attributesTree = CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, options: options);

            comp = CreateCompilation([source, interceptors, attributesTree]);

            if (expectedOutput == null)
            {
                comp.VerifyEmitDiagnostics(expected);
            }
            else
            {
                CompileAndVerify(comp, expectedOutput: expectedOutput)
                    .VerifyDiagnostics(expected);
            }
        }
    }

    [Fact]
    public void FeatureFlag_Granular_02()
    {
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            namespace NS1.NS2
            {
                class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void M() => Console.Write(1);
                }
            }
            """;

        sadCase("NS2");
        sadCase("true");
        sadCase(" NS1");
        sadCase(";");
        sadCase(";;");
        sadCase("");
        sadCase("NS1 ;");
        sadCase("NS1..NS2;");
        sadCase("ns1");
        sadCase("NS2.NS1");
        sadCase("$NS1&");

        happyCase("NS1");
        happyCase("NS1;");
        happyCase(";NS1");
        happyCase("NS1.NS2");
        happyCase("NS2;NS1.NS2");
        happyCase("NS2;;NS1.NS2");

        void sadCase(string featureValue)
        {
            var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", featureValue));
            comp.VerifyEmitDiagnostics(
                // (8,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1.NS2</InterceptorsNamespaces>' to your project.
                //         [InterceptsLocation(1, "eY+urAo7Kg2rsKgGSGjShwIAAAA=")]
                Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NS1.NS2</InterceptorsNamespaces>").WithLocation(8, 10));
        }

        void happyCase(string featureValue)
        {
            var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", featureValue), expectedOutput: "1");
            verifier.VerifyDiagnostics();
        }
    }

    [Fact]
    public void FeatureFlag_Granular_03()
    {
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M() => Console.Write(1);
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", ""));
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9206: An interceptor cannot be declared in the global namespace.
            //     [InterceptsLocation(1, "eY+urAo7Kg2rsKgGSGjShwIAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorGlobalNamespace, "InterceptsLocation").WithLocation(6, 6));
    }

    [Fact]
    public void FeatureFlag_Granular_04()
    {
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;
            namespace global
            {
                class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void M() => Console.Write(1);
                }
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "global"), expectedOutput: "1");
        verifier.VerifyDiagnostics();

        verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "global"), expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void FeatureFlag_Granular_05()
    {
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;

        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            namespace global.B
            {
                class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void M() => Console.Write(1);
                }
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "global.A"));
        comp.VerifyEmitDiagnostics(
            // (8,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);global.B</InterceptorsNamespaces>' to your project.
            //         [InterceptsLocation(1, "eY+urAo7Kg2rsKgGSGjShwIAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);global.B</InterceptorsNamespaces>").WithLocation(8, 10));
    }

    [Fact]
    public void SelfInterception()
    {
        var source = """
            using System;

            partial class C
            {
                public static void Main()
                {
                    InterceptableMethod();
                }

                public static partial void InterceptableMethod() { Console.Write(1); }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            partial class C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static partial void InterceptableMethod();
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void StaticInterceptable_StaticInterceptor_NoParameters()
    {
        var source = """
            using System;

            class C
            {

                public static void InterceptableMethod() { Console.Write("interceptable"); }

                public static void Main()
                {
                    InterceptableMethod();
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1() { Console.Write("interceptor 1"); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor 1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void Accessibility_01()
    {
        var source = """
            using System;

            class C
            {

                public static void InterceptableMethod() { Console.Write("interceptable"); }

                public static void Main()
                {
                    InterceptableMethod();
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                private static void Interceptor1() { Console.Write("interceptor 1"); }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9155: Cannot intercept call with 'D.Interceptor1()' because it is not accessible within 'C.Main()'.
            //     [InterceptsLocation(1, "BKq4YXWKYsMUMsR6wdliFJkAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorNotAccessible, "InterceptsLocation").WithArguments("D.Interceptor1()", "C.Main()").WithLocation(6, 6));
    }

    [Fact]
    public void Accessibility_02()
    {
        // An interceptor declared within a file-local type can intercept a call even if the call site can't normally refer to the file-local type.
        var source1 = """
            using System;

            class C
            {

                public static void InterceptableMethod() { Console.Write("interceptable"); }

                public static void Main()
                {
                    InterceptableMethod();
                }
            }
            """;
        var locations = GetInterceptableLocations(source1);
        var source2 = $$"""
            using System.Runtime.CompilerServices;
            using System;

            file class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1() { Console.Write("interceptor 1"); }
            }
            """;

        var verifier = CompileAndVerify(new[] { (source1, "Program.cs"), (source2, "Other.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "interceptor 1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void FileLocalAttributeDefinitions_01()
    {
        // Treat a file-local declaration of InterceptsLocationAttribute as a well-known attribute within the declaring compilation.
        var source = """
            C.M();

            class C
            {
                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M()
                {
                    Console.Write(1);
                }
            }

            namespace System.Runtime.CompilerServices
            {
                file class InterceptsLocationAttribute : Attribute
                {
                    public InterceptsLocationAttribute(int version, string data) { }
                }
            }
            """;

        var verifier = CompileAndVerify([source, (interceptors, "Interceptors.cs")], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    /// <summary>
    /// File-local InterceptsLocationAttribute from another compilation is not considered to *duplicate* an interception, even if it is inherited.
    /// See also <see cref="DuplicateLocation_03"/>.
    /// </summary>
    [Fact]
    public void FileLocalAttributeDefinitions_02()
    {
        var source1 = """
            using System.Runtime.CompilerServices;
            using System;

            var c = new C();
            c.M();

            public class C
            {
                public void M() => Console.Write(1);

                [InterceptsLocation("Program.cs", 5, 3)]
                public virtual void Interceptor() => throw null!;
            }

            namespace System.Runtime.CompilerServices
            {
                [AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
                file sealed class InterceptsLocationAttribute : Attribute
                {
                    public InterceptsLocationAttribute(string filePath, int line, int character)
                    {
                    }
                }
            }
            """;

        // Inherited attribute on 'override void Interceptor' from other compilation doesn't cause a call in this compilation to be intercepted.
        var source2 = """


            // leading blank lines for alignment with the call in the other compilation.
            var d = new D();
            d.M();

            class D : C
            {
                public override void Interceptor() => throw null!;
            }
            """;

        var comp1 = CreateCompilation((source1, "Program.cs"), parseOptions: RegularWithInterceptors);
        comp1.VerifyEmitDiagnostics(
            // Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 5, 3)").WithLocation(11, 6));

        var comp2Verifier = CompileAndVerify((source2, "Program.cs"), references: new[] { comp1.ToMetadataReference() }, parseOptions: RegularWithInterceptors, expectedOutput: "1");
        comp2Verifier.VerifyDiagnostics(
            // Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 5, 3)").WithLocation(11, 6));

        comp2Verifier = CompileAndVerify((source2, "Program.cs"), references: new[] { comp1.EmitToImageReference() }, parseOptions: RegularWithInterceptors, expectedOutput: "1");
        comp2Verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableExtensionMethod_InterceptorExtensionMethod()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call site");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableExtensionMethod_InterceptorExtensionMethod_NormalForm()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    InterceptableMethod(c, "call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call site");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableInstanceMethod_InterceptorExtensionMethod()
    {
        var source = """
            using System;

            class C
            {

                public C InterceptableMethod(string param) { Console.Write("interceptable " + param); return this; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static C Interceptor1(this C i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call site");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableInstanceMethod_InterceptorStaticMethod()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {

                public C InterceptableMethod(string param) { Console.Write("interceptable " + param); return this; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static C Interceptor1(C i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9144: Cannot intercept method 'C.InterceptableMethod(string)' with interceptor 'D.Interceptor1(C, string)' because the signatures do not match.
            //     [InterceptsLocation(1, "++/BPYeNndnfOx03gyhygBkBAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.InterceptableMethod(string)", "D.Interceptor1(C, string)").WithLocation(6, 6)
            );
    }

    [Fact]
    public void InterceptsLocationDuplicatePath()
    {
        var source0 = ("""
            public class D0
            {
                public static void M()
                {
                    C.InterceptableMethod("a");
                }
            }
            """, "Program.cs");

        var source1 = ("""
            public class D1
            {
                public static void M()
                {
                    C.InterceptableMethod("a");
                }
            }
            """, "Program.cs");

        var source2 = ("""
            using System.Runtime.CompilerServices;
            using System;

            D0.M();
            D1.M();

            public class C
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }
            }

            public static class Interceptor
            {
                [InterceptsLocation("Program.cs", 5, 11)]
                public static void Interceptor1(string param) { Console.Write("interceptor " + param); }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source0, source1, source2, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(15,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 5, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 5, 11)").WithLocation(15, 6),
            // Interceptor.cs(15,25): error CS9152: Cannot intercept a call in file with path 'Program.cs' because multiple files in the compilation have this path.
            //     [InterceptsLocation("Program.cs", 5, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorNonUniquePath, @"""Program.cs""").WithArguments("Program.cs").WithLocation(15, 25));
    }

    [Fact]
    public void DuplicateLocation_01()
    {
        var source = """
            C.M();

            class C
            {

                public static void M() { }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1() { }

                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M2() { }
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "W99OXsRRPXziuK607Sn0QgIAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(5, 6),
            // (8,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "W99OXsRRPXziuK607Sn0QgIAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(8, 6)
        );
    }

    [Fact]
    public void DuplicateLocation_02()
    {
        var source0 = """
            using System.Runtime.CompilerServices;

            C.M();

            class C
            {

                public static void M() { }
            }
            """;
        var locations = GetInterceptableLocations(source0);

        var source1 = $$"""
            using System.Runtime.CompilerServices;

            class D1
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1() { }
            }
            """;

        var source2 = $$"""
            using System.Runtime.CompilerServices;

            class D2
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1() { }
            }
            """;

        var comp = CreateCompilation(new[] { (source0, "Program.cs"), (source1, "File1.cs"), (source2, "File2.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // File2.cs(5,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "n2BejMbKpTRExveL7QXL7CwAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(5, 6),
            // File1.cs(5,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "n2BejMbKpTRExveL7QXL7CwAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(5, 6)
            );
    }

    [Fact]
    public void DuplicateLocation_03()
    {
        // InterceptsLocationAttribute is not considered to *duplicate* an interception, even if it is inherited.
        var source = """
            using System;

            var d = new D();
            d.M();

            partial class C
            {

                public void M() => throw null!;

                public virtual partial void Interceptor() => throw null!;
            }

            class D : C
            {
                public override void Interceptor() => Console.Write(1);
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            partial class C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public virtual partial void Interceptor();
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void DuplicateLocation_04()
    {
        var source = """
            using System.Runtime.CompilerServices;

            C.M();

            class C
            {

                public static void M() { }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1() { }
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "n2BejMbKpTRExveL7QXL7CwAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(6, 6),
            // (7,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "n2BejMbKpTRExveL7QXL7CwAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(7, 6));
    }

    [Fact]
    public void InterceptorVirtual_01()
    {
        // Intercept a method call with a call to a virtual method on the same type.
        var source = """
            using System;

            C c = new C();
            c.M();

            c = new D();
            c.M();

            partial class C
            {
                public void M() => throw null!;
            }

            class D : C
            {
                public override void Interceptor() => Console.Write("D");
            }
            """;

        var locations = GetInterceptableLocations(source);

        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            partial class C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public virtual void Interceptor() => Console.Write("C");
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "CD");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptorVirtual_02()
    {
        // Intercept a call with a virtual method call on the base type.
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            D d = new D();
            d.M();

            partial class C
            {
                public virtual partial void Interceptor();
            }

            class D : C
            {

                public void M() => throw null!;

                public override void Interceptor() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            partial class C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public virtual partial void Interceptor() => throw null!;
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'D this' on 'D.M()'.
            //     [InterceptsLocation(1, "HOfsJKA9cGIUJFWxsV9jeksAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("D this", "D.M()").WithLocation(6, 6));
    }

    [Fact]
    public void InterceptorOverride_01()
    {
        // Intercept a call with a call to an override method on a derived type.
        var source = """
            D d = new D();
            d.M();

            class C
            {

                public void M() => throw null!;

                public virtual void Interceptor() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D : C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public override void Interceptor() => throw null!;
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'C this' on 'C.M()'.
            //     [InterceptsLocation(1, "u4STVrPS9MrXo2LQRYzzABIAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("C this", "C.M()").WithLocation(6, 6)
            );
    }

    [Fact]
    public void InterceptorOverride_02()
    {
        // Intercept a call with an override method on the same type.
        var source = """
            D d = new D();
            d.M();

            class C
            {
                public virtual void Interceptor() => throw null!;
            }

            partial class D : C
            {
                public void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            partial class D : C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public override void Interceptor() => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void EmitMetadataOnly_01()
    {
        // We can emit a ref assembly even though there are duplicate interceptions.
        var source = """
            class C
            {
                public static void Main()
                {
                    C.M();
                }


                public static void M() { }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1() { }

                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M2() { }
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, emitOptions: EmitOptions.Default.WithEmitMetadataOnly(true));
        verifier.VerifyDiagnostics();

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "K5uRlX0Frr/Ngo5L9TVTNTwAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(5, 6),
            // (8,6): error CS9153: The indicated call is intercepted multiple times.
            //     [InterceptsLocation(1, "K5uRlX0Frr/Ngo5L9TVTNTwAAAA=")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(8, 6));
    }

    [Fact]
    public void EmitMetadataOnly_02()
    {
        // We can't emit a ref assembly when a problem is found with an InterceptsLocationAttribute in the declaration phase.
        // Strictly, we should perhaps allow this emit anyway, but it doesn't feel urgent to do so.
        var source = """
            using System.Runtime.CompilerServices;

            C.M();

            class C
            {

                public static void M() { }
            }

            class D
            {
                [InterceptsLocation(1, "ERROR")]
                public static void M1() { }

            }
            """;

        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(EmitOptions.Default.WithEmitMetadataOnly(true),
            // Program.cs(13,6): error CS9231: The data argument to InterceptsLocationAttribute is not in the correct format.
            //     [InterceptsLocation(1, "ERROR")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidFormat, "InterceptsLocation").WithLocation(13, 6));
    }

    [Fact]
    public void InterceptsLocationFromMetadata()
    {
        // Verify that `[InterceptsLocation]` on a method from metadata does not cause a call in the current compilation to be intercepted.
        var source0 = """
            using System.Runtime.CompilerServices;
            using System;

            public class C0
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }

                static void M0()
                {
                    InterceptableMethod("1");
                }
            }

            public static class D
            {
                [InterceptsLocation("Program.cs", 11, 9)]
                public static void Interceptor1(string param) { Console.Write("interceptor " + param); }
            }
            """;
        var comp0 = CreateCompilation(new[] { (source0, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp0.VerifyEmitDiagnostics(
            // Program.cs(17,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 11, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 11, 9)").WithLocation(17, 6));

        var source1 = """

            using System;

            class C1
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }

                static void Main()
                {
                    InterceptableMethod("1");
                }
            }
            """;

        var comp1 = CompileAndVerify(new[] { (source1, "Program.cs") }, new[] { comp0.ToMetadataReference() }, parseOptions: RegularWithInterceptors, expectedOutput: "interceptable 1");
        comp1.VerifyDiagnostics(
            // Program.cs(17,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 11, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 11, 9)").WithLocation(17, 6));

        comp1 = CompileAndVerify(new[] { (source1, "Program.cs") }, new[] { comp0.EmitToImageReference() }, parseOptions: RegularWithInterceptors, expectedOutput: "interceptable 1");
        comp1.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableDelegateConversion()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {

                public C InterceptableMethod(string param) { Console.Write("interceptable " + param); return this; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    var del = c.InterceptableMethod;
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 15, 21)]
                public static C Interceptor1(this C i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var compilation = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        compilation.VerifyEmitDiagnostics(
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 15, 21)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 15, 21)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9151: Possible method name 'InterceptableMethod' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 15, 21)]
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 15, 21)").WithArguments("InterceptableMethod").WithLocation(21, 6)
            );
    }

    [Fact]
    public void InterceptableNameof()
    {
        var source = """
            static class Program
            {
                public static void Main()
                {
                    _ = nameof(Main);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(object param) { }
            }
            """;
        var compilation = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        compilation.VerifyEmitDiagnostics(
            // (5,13): error CS9160: A nameof operator cannot be intercepted.
            //         _ = nameof(Main);
            Diagnostic(ErrorCode.ERR_InterceptorCannotInterceptNameof, "nameof").WithLocation(5, 13)
            );
    }

    [Fact]
    public void InterceptableNameof_MethodCall()
    {
        var source = """
            static class Program
            {
                public static void Main()
                {
                    _ = nameof(F);
                }

                private static object F = 1;


                public static string nameof(object param) => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static string Interceptor1(object param)
                {
                    Console.Write(1);
                    return param.ToString();
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableDoubleUnderscoreReservedIdentifiers()
    {
        // Verify that '__arglist', '__makeref', '__refvalue', and '__reftype' cannot be intercepted.
        // Because the APIs for obtaining InterceptableLocation don't work with these constructs, we have effectively blocked it.
        var source = CSharpTestSource.Parse("""
            using System.Runtime.CompilerServices;
            using System;

            static class Program
            {
                public static void Main()
                {
                    M1(__arglist(1, 2, 3));

                    int i = 0;
                    TypedReference tr = __makeref(i);
                    ref int ri = ref __refvalue(tr, int);
                    Type t = __reftype(tr);
                }

                static void M1(__arglist) { }
            }
            """, "Program.cs", options: RegularWithInterceptors);

        Assert.Collection(source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>(),
            first => Assert.Equal("M1(__arglist(1, 2, 3))", first.ToString()),
            second => Assert.Equal("__arglist(1, 2, 3)", second.ToString()));

        Assert.Collection(GetInterceptableLocations(source),
            first => Assert.Equal("Program.cs(8,9)", first!.GetDisplayLocation()),
            second => Assert.Null(second));
    }

    [Fact]
    public void InterceptableDelegateInvocation_01()
    {
        var source = """
            using System;

            C.M(() => Console.Write(1));
            C.M1((() => Console.Write(1), 0));

            static class C
            {
                public static void M(Action action)
                {
                    action();
                }

                public static void M1((Action action, int) pair)
                {
                    pair.action();
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[4]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[5]!)}})]
                public static void Interceptor1(this Action action) { action(); Console.Write(2); }
            }
            """;
        var compilation = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        compilation.VerifyEmitDiagnostics(
            // (6,6): error CS9207: Cannot intercept 'action' because it is not an invocation of an ordinary member method.
            //     [InterceptsLocation(1, "OC8Ntn0ZsekhqswDcyGy6ZgAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptableMethodMustBeOrdinary, "InterceptsLocation").WithArguments("action").WithLocation(6, 6),
            // (7,6): error CS9207: Cannot intercept 'action' because it is not an invocation of an ordinary member method.
            //     [InterceptsLocation(1, "OC8Ntn0ZsekhqswDcyGy6f4AAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptableMethodMustBeOrdinary, "InterceptsLocation").WithArguments("action").WithLocation(7, 6));
    }

    [Fact]
    public void InterceptableDelegateInvocation_02()
    {
        var source = """
            using System;

            C.M(() => Console.Write(1));
            C.M1((() => Console.Write(1), 0));

            static class C
            {
                public static void M(Action action)
                {
                    action!();
                }

                public static void M1((Action action, int) pair)
                {
                    pair.action!();
                }
            }
            """;

        // 'action!' syntactic form prevents obtaining a location for these.
        // They semantically cannot be intercepted so we don't really care.
        var locations = GetInterceptableLocations(source);
        Assert.Null(locations[4]);
        Assert.Null(locations[5]);

        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation("Program.cs", 10, 9)]
                [InterceptsLocation("Program.cs", 15, 14)]
                public static void Interceptor1(this Action action) { action(); Console.Write(2); }
            }
            """;
        var compilation = CreateCompilation([(source, "Program.cs"), interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        compilation.VerifyEmitDiagnostics(
            // (6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 10, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 10, 9)").WithLocation(6, 6),
            // (6,6): error CS9151: Possible method name 'action' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 10, 9)]
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 10, 9)").WithArguments("action").WithLocation(6, 6),
            // (7,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 15, 14)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 15, 14)").WithLocation(7, 6),
            // (7,6): error CS9151: Possible method name 'action' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 15, 14)]
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 15, 14)").WithArguments("action").WithLocation(7, 6));
    }

    [Fact]
    public void QualifiedNameAtCallSite()
    {
        var source = """
            using System;

            class C
            {

                public static C InterceptableMethod(C c, string param) { Console.Write("interceptable " + param); return c; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    C.InterceptableMethod(c, "call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static C Interceptor1(C c, string param) { Console.Write("interceptor " + param); return c; }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call site");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableStaticMethod_InterceptorExtensionMethod()
    {
        var source = """
            using System;

            class C
            {

                public static C InterceptableMethod(C c, string param) { Console.Write("interceptable " + param); return c; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    C.InterceptableMethod(c, "call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call site");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableExtensionMethod_InterceptorStaticMethod()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            var c = new C();
            c.InterceptableMethod();

            class C { }

            static partial class D
            {

                public static void InterceptableMethod(this C c) => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(C c) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'C c' on 'D.InterceptableMethod(C)'.
            //     [InterceptsLocation(1, "4eoe0tUG+oqPzA8jHIWbdU0AAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("C c", "D.InterceptableMethod(C)").WithLocation(6, 6));
    }

    [Fact]
    public void InterceptableExtensionMethod_InterceptorStaticMethod_NormalForm()
    {
        var source = """
            var c = new C();
            D.InterceptableMethod(c);

            class C { }

            static partial class D
            {
                public static void InterceptableMethod(this C c) => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(C c) => Console.Write(1);
            }
            """;
        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableStaticMethod_InterceptorInstanceMethod()
    {
        var source = """
            using System;

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod("call site");
                }
            }

            partial class C
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            partial class C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public void Interceptor1(string param) { Console.Write("interceptor " + param); }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9149: Interceptor must not have a 'this' parameter because 'C.InterceptableMethod(string)' does not have a 'this' parameter.
            //     [InterceptsLocation(1, "s3IopQ8OwA+tKaUOHzxvAFoAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustNotHaveThisParameter, "InterceptsLocation").WithArguments("C.InterceptableMethod(string)").WithLocation(6, 6));
    }

    [Fact]
    public void ArgumentLabels()
    {
        var source = """
            using System;

            class C
            {

                public void InterceptableMethod(string s1, string s2) { Console.Write(s1 + s2); }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod(s2: "World", s1: "Hello ");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1(this C c, string s1, string s2) { Console.Write("interceptor " + s1 + s2); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor Hello World");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void ParameterNameDifference()
    {
        var source = """
            class C
            {

                public void InterceptableMethod(string s1) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod(s1: "1");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(this C c, string s2) { Console.Write(s2); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void ParameterNamesInDifferentOrder()
    {
        var source = """
            class C
            {

                public void InterceptableMethod(string s1, string s2) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("1", "2");
                    c.InterceptableMethod(s2: "4", s1: "3");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1(this C c, string s2, string s1) { Console.Write(s2); Console.Write(s1); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1234");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void AttributeArgumentLabels_01()
    {
        var source = """
            class C
            {

                public void InterceptableMethod() => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod();
                }
            }
            """;
        var location = GetInterceptableLocations(source)[0]!;
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation(version: {{location.Version}}, data: "{{location.Data}}")]
                public static void Interceptor1(this C c) { Console.Write(1); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void AttributeArgumentLabels_02()
    {
        var source = """
            class C
            {

                public void InterceptableMethod() => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod();
                }
            }
            """;
        var location = GetInterceptableLocations(source)[0]!;
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation(data: "{{location.Data}}", version: {{location.Version}})] // 1
                public static void Interceptor1(this C c) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableExtensionMethod_InterceptorExtensionMethod_Sequence()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site")
                        .InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call siteinterceptable call site");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableFromMetadata()
    {
        var source1 = """

            using System;

            public class C
            {

                public C InterceptableMethod(string param) { Console.Write("interceptable " + param); return this; }
            }
            """;

        var source2 = """
            using System.Runtime.CompilerServices;
            using System;

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 9, 11)]
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }
            """;

        var comp1 = CreateCompilation(new[] { (source1, "File1.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp1.VerifyEmitDiagnostics();

        var verifier = CompileAndVerify((source2, "Program.cs"), references: new[] { comp1.ToMetadataReference() }, parseOptions: RegularWithInterceptors, expectedOutput: "interceptor call site");
        verifier.VerifyDiagnostics(
            // Program.cs(15,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 9, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 9, 11)").WithLocation(15, 6));
    }

    [Fact]
    public void InterceptsLocation_BadMethodKind()
    {
        var source = """
            static partial class Program
            {

                public static void InterceptableMethod(string param) { }

                public static void Main()
                {
                    InterceptableMethod("");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            #pragma warning disable 8321 // The local function is declared but never used
            using System.Runtime.CompilerServices;

            static partial class Program
            {
                static void M()
                {
                    var lambda = [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] (string param) => { }; // 1

                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 2
                    static void Interceptor1(string param) { }
                }

                public static string Prop
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 3
                    set { }
                }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyDiagnostics(
            // (8,23): error CS9146: An interceptor method must be an ordinary member method.
            //         var lambda = [InterceptsLocation(1, "Od9e6GAEIdSlUyHQlAJMLIkAAAA=")] (string param) => { }; // 1
            Diagnostic(ErrorCode.ERR_InterceptorMethodMustBeOrdinary, "InterceptsLocation").WithLocation(8, 23),
            // (10,10): error CS9146: An interceptor method must be an ordinary member method.
            //         [InterceptsLocation(1, "Od9e6GAEIdSlUyHQlAJMLIkAAAA=")] // 2
            Diagnostic(ErrorCode.ERR_InterceptorMethodMustBeOrdinary, "InterceptsLocation").WithLocation(10, 10),
            // (16,10): error CS9146: An interceptor method must be an ordinary member method.
            //         [InterceptsLocation(1, "Od9e6GAEIdSlUyHQlAJMLIkAAAA=")] // 3
            Diagnostic(ErrorCode.ERR_InterceptorMethodMustBeOrdinary, "InterceptsLocation").WithLocation(16, 10)
            );
    }

    [Fact]
    public void InterceptsLocation_BadMethodKind_Checksum()
    {
        var source = CSharpTestSource.Parse("""
            class Program
            {
                public static void InterceptableMethod(string param) { }

                public static void Main()
                {
                    InterceptableMethod("");
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var invocation = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var model = comp.GetSemanticModel(source);
        var location = model.GetInterceptableLocation(invocation)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System.Runtime.CompilerServices;

            class C
            {
                static void M()
                {
                    Interceptor1("");
                    var lambda = [InterceptsLocation({{location.Version}}, "{{location.Data}}")] (string param) => { }; // 1

                    [InterceptsLocation({{location.Version}}, "{{location.Data}}")] // 2
                    static void Interceptor1(string param) { }
                }

                public static string Prop
                {
                    [InterceptsLocation({{location.Version}}, "{{location.Data}}")] // 3
                    set { }
                }
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, s_attributesTree]);
        comp.VerifyDiagnostics(
            // Interceptors.cs(8,23): error CS9146: An interceptor method must be an ordinary member method.
            //         var lambda = [InterceptsLocation(1, "OjpNlan67EMibFykRLWBLXgAAABQcm9ncmFtLmNz")] (string param) => { }; // 1
            Diagnostic(ErrorCode.ERR_InterceptorMethodMustBeOrdinary, "InterceptsLocation").WithLocation(8, 23),
            // Interceptors.cs(10,10): error CS9146: An interceptor method must be an ordinary member method.
            //         [InterceptsLocation(1, "OjpNlan67EMibFykRLWBLXgAAABQcm9ncmFtLmNz")] // 2
            Diagnostic(ErrorCode.ERR_InterceptorMethodMustBeOrdinary, "InterceptsLocation").WithLocation(10, 10),
            // Interceptors.cs(16,10): error CS9146: An interceptor method must be an ordinary member method.
            //         [InterceptsLocation(1, "OjpNlan67EMibFykRLWBLXgAAABQcm9ncmFtLmNz")] // 3
            Diagnostic(ErrorCode.ERR_InterceptorMethodMustBeOrdinary, "InterceptsLocation").WithLocation(16, 10)
            );
    }

    [Fact]
    public void InterceptableMethod_BadMethodKind_01()
    {
        var source = """
            using System.Runtime.CompilerServices;

            class Program
            {
                public static unsafe void Main()
                {
                    // property
                    _ = Prop;

                    // constructor
                    new Program();
                }

                public static int Prop { get; }

                [InterceptsLocation("Program.cs", 8, 13)] // 1
                [InterceptsLocation("Program.cs", 11, 9)] // 2, 'new'
                [InterceptsLocation("Program.cs", 11, 13)] // 3, 'Program'
                static void Interceptor1() { }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors, options: TestOptions.UnsafeDebugExe);
        comp.VerifyDiagnostics(
            // Program.cs(16,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 8, 13)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 8, 13)").WithLocation(16, 6),
            // Program.cs(16,6): error CS9151: Possible method name 'Prop' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 8, 13)] // 1
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 8, 13)").WithArguments("Prop").WithLocation(16, 6),
            // Program.cs(17,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 11, 9)] // 2, 'new'
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 11, 9)").WithLocation(17, 6),
            // Program.cs(17,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token 'new'.
            //     [InterceptsLocation("Program.cs", 11, 9)] // 2, 'new'
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 11, 9)").WithArguments("new").WithLocation(17, 6),
            // Program.cs(18,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 11, 13)] // 3, 'Program'
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 11, 13)").WithLocation(18, 6),
            // Program.cs(18,6): error CS9151: Possible method name 'Program' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 11, 13)] // 3, 'Program'
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 11, 13)").WithArguments("Program").WithLocation(18, 6)
            );
    }

    [Fact]
    public void InterceptableMethod_BadMethodKind_Checksum_01()
    {
        var source = CSharpTestSource.Parse("""
            class Program
            {
                public static void Main()
                {
                    // property
                    _ = Prop; // 1 ('Prop')

                    // constructor
                    new Program(); // 2 ('new'), 3 ('Program')
                }

                public static int Prop { get; }
            }
            """, "Program.cs", options: RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = (CSharpSemanticModel)comp.GetSemanticModel(source);
        var root = source.GetRoot();

        var node1 = root.DescendantNodes().First(node => node is IdentifierNameSyntax name && name.Identifier.Text == "Prop");
        var location1 = model.GetInterceptableLocationInternal(node1, cancellationToken: default);

        var node2 = root.DescendantNodes().Single(node => node is ObjectCreationExpressionSyntax);
        var location2 = model.GetInterceptableLocationInternal(node2, cancellationToken: default);

        var node3 = root.DescendantNodes().Last(node => node is IdentifierNameSyntax name && name.Identifier.Text == "Program");
        var location3 = model.GetInterceptableLocationInternal(node3, cancellationToken: default);

        var interceptors = CSharpTestSource.Parse($$"""
            using System.Runtime.CompilerServices;

            class C
            {
                [InterceptsLocation({{location1.Version}}, "{{location1.Data}}")] // 1
                [InterceptsLocation({{location2.Version}}, "{{location2.Data}}")] // 2
                [InterceptsLocation({{location3.Version}}, "{{location3.Data}}")] // 3
                static void Interceptor1() { }
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, s_attributesTree]);
        comp.VerifyDiagnostics(
            // Interceptors.cs(5,6): error CS9151: Possible method name 'Prop' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation(1, "hD44wQkJk1har7RM7oznpFkAAABQcm9ncmFtLmNz")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, "InterceptsLocation").WithArguments("Prop").WithLocation(5, 6),
            // Interceptors.cs(6,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token 'new'.
            //     [InterceptsLocation(1, "hD44wQkJk1har7RM7oznpG4AAABQcm9ncmFtLmNz")] // 2
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, "InterceptsLocation").WithArguments("new").WithLocation(6, 6),
            // Interceptors.cs(7,6): error CS9151: Possible method name 'Program' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation(1, "hD44wQkJk1har7RM7oznpJQAAABQcm9ncmFtLmNz")] // 3
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, "InterceptsLocation").WithArguments("Program").WithLocation(7, 6)
            );
    }

    [Fact]
    public void InterceptableMethod_BadMethodKind_02()
    {
        var source = """
            using System;

            partial class Program
            {
                public static unsafe void Main()
                {
                    // delegate
                    Action a = () => throw null!;
                    a();

                    // local function
                    void local() => throw null!;
                    local();

                    // fnptr invoke
                    delegate*<void> fnptr = &Interceptor1;
                    fnptr();
                }

                public static int Prop { get; }

                static partial void Interceptor1() { }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            partial class Program
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 2
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})] // 3
                static partial void Interceptor1();
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, options: TestOptions.UnsafeDebugExe);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9207: Cannot intercept 'a' because it is not an invocation of an ordinary member method.
            //     [InterceptsLocation(1, "ugKu5/LV5oAEk8GTsnS0hJQAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptableMethodMustBeOrdinary, "InterceptsLocation").WithArguments("a").WithLocation(5, 6),
            // (6,6): error CS9207: Cannot intercept 'local' because it is not an invocation of an ordinary member method.
            //     [InterceptsLocation(1, "ugKu5/LV5oAEk8GTsnS0hOUAAAA=")] // 2
            Diagnostic(ErrorCode.ERR_InterceptableMethodMustBeOrdinary, "InterceptsLocation").WithArguments("local").WithLocation(6, 6),
            // (7,6): error CS9207: Cannot intercept 'fnptr' because it is not an invocation of an ordinary member method.
            //     [InterceptsLocation(1, "ugKu5/LV5oAEk8GTsnS0hEIBAAA=")] // 3
            Diagnostic(ErrorCode.ERR_InterceptableMethodMustBeOrdinary, "InterceptsLocation").WithArguments("fnptr").WithLocation(7, 6)
            );
    }

    [Fact]
    public void InterceptorCannotBeGeneric_01()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1
            {

                public I1 InterceptableMethod(string param) { Console.Write("interceptable " + param); return this; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static I1 Interceptor1<T>(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9178: Method 'D.Interceptor1<T>(I1, string)' must be non-generic to match 'Console.Write(string)'.
            //     [InterceptsLocation(1, "ASfq/xnhlb1QGHJAQ5lqJXAAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorCannotBeGeneric, "InterceptsLocation").WithArguments("D.Interceptor1<T>(I1, string)", "System.Console.Write(string)").WithLocation(6, 6));
    }

    [Fact]
    public void InterceptorCannotBeGeneric_02()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D<T>
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(string param) { Console.Write("interceptor " + param); }
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9138: Method 'D<T>.Interceptor1(string)' cannot be used as an interceptor because its containing type has type parameters.
            //     [InterceptsLocation(1, "ZCdvmiprtZ938pueLU5g6HkAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorContainingTypeCannotBeGeneric, "InterceptsLocation").WithArguments("D<T>.Interceptor1(string)").WithLocation(6, 6));
    }

    [Fact]
    public void InterceptorCannotBeGeneric_Checksum_02()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            interface I1 { }
            class C : I1
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod("call site");
                }
            }
            """, "Program.cs", options: RegularWithInterceptors);
        var comp = CreateCompilation(source);
        comp.VerifyDiagnostics();

        var invocation = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var model = comp.GetSemanticModel(source);
        var location = model.GetInterceptableLocation(invocation)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            static class D<T>
            {
                {{location.GetInterceptsLocationAttributeSyntax()}}
                public static void Interceptor1(string param) { Console.Write("interceptor " + param); }
            }
            """, "Interceptors.cs", options: RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, s_attributesTree]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(5,6): error CS9138: Method 'D<T>.Interceptor1(string)' cannot be used as an interceptor because its containing type has type parameters.
            //     [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "ZCdvmiprtZ938pueLU5g6OsAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorContainingTypeCannotBeGeneric, "global::System.Runtime.CompilerServices.InterceptsLocationAttribute").WithArguments("D<T>.Interceptor1(string)").WithLocation(5, 6));
    }

    [Fact]
    public void InterceptorCannotBeGeneric_03()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1
            {

                public static void InterceptableMethod(string param) { Console.Write("interceptable " + param); }
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class Outer<T>
            {
                static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                    public static void Interceptor1(string param) { Console.Write("interceptor " + param); }
                }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (8,10): error CS9138: Method 'Outer<T>.D.Interceptor1(string)' cannot be used as an interceptor because its containing type has type parameters.
            //         [InterceptsLocation(1, "ZCdvmiprtZ938pueLU5g6OsAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorContainingTypeCannotBeGeneric, "InterceptsLocation").WithArguments("Outer<T>.D.Interceptor1(string)").WithLocation(8, 10)
            );
    }

    [Fact]
    public void InterceptableGeneric_01()
    {
        var source = """
            using System;

            class C
            {
                public static void InterceptableMethod<T>(T t) { Console.Write("0"); }
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod<string>("1");
                    C.InterceptableMethod("2");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                public static void Interceptor1(string s) { Console.Write(s); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "12");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_02()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {

                public static void InterceptableMethod<T>(T t) { Console.Write("0"); }
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod<string>("1");
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 14, 30)]
                [InterceptsLocation("Program.cs", 14, 31)]
                [InterceptsLocation("Program.cs", 14, 37)]
                public static void Interceptor1(string s) { Console.Write(s); }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(20,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 14, 30)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 14, 30)").WithLocation(20, 6),
            // Program.cs(20,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token '<'.
            //     [InterceptsLocation("Program.cs", 14, 30)]
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 14, 30)").WithArguments("<").WithLocation(20, 6),
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 14, 31)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 14, 31)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token 'string'.
            //     [InterceptsLocation("Program.cs", 14, 31)]
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 14, 31)").WithArguments("string").WithLocation(21, 6),
            // Program.cs(22,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 14, 37)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 14, 37)").WithLocation(22, 6),
            // Program.cs(22,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token '>'.
            //     [InterceptsLocation("Program.cs", 14, 37)]
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 14, 37)").WithArguments(">").WithLocation(22, 6)
            );
    }

    [Fact]
    public void InterceptableGeneric_03()
    {
        var source = """
            class C
            {

                public static void InterceptableMethod<T>(T t) where T : class => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod<string>("1");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;
            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(string s) { Console.Write(s); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_04()
    {
        var source = """
            class C
            {

                public static void InterceptableMethod<T1>(T1 t) => throw null!;
            }

            static class Program
            {
                public static void M<T2>(T2 t)
                {
                    C.InterceptableMethod(t);
                    C.InterceptableMethod<T2>(t);
                    C.InterceptableMethod<object>(t);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 2
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                public static void Interceptor1(object s) { Console.Write(s); }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9144: Cannot intercept method 'C.InterceptableMethod<T2>(T2)' with interceptor 'D.Interceptor1(object)' because the signatures do not match.
            //     [InterceptsLocation(1, "GRj3gKijugIAuusp6isB1qcAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.InterceptableMethod<T2>(T2)", "D.Interceptor1(object)").WithLocation(6, 6),
            // (7,6): error CS9144: Cannot intercept method 'C.InterceptableMethod<T2>(T2)' with interceptor 'D.Interceptor1(object)' because the signatures do not match.
            //     [InterceptsLocation(1, "GRj3gKijugIAuusp6isB1soAAAA=")] // 2
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.InterceptableMethod<T2>(T2)", "D.Interceptor1(object)").WithLocation(7, 6)
            );
    }

    [Fact]
    public void InterceptableGeneric_05()
    {
        var source = """
            C.Usage(1);
            C.Usage(2);

            class C
            {
                public static void InterceptableMethod<T1>(T1 t) => throw null!;

                public static void Usage<T2>(T2 t)
                {
                    C.InterceptableMethod(t);
                    C.InterceptableMethod<T2>(t);
                    C.InterceptableMethod<object>(t);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[4]!)}})]
                public static void Interceptor1<T>(T t) { Console.Write(t); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "111222");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_06()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public static void InterceptableMethod<T1>(T1 t) => throw null!;

                public static void Usage()
                {
                    C.InterceptableMethod("abc");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static void Interceptor1<T>(T t) where T : struct => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS0453: The type 'string' must be a non-nullable value type in order to use it as parameter 'T' in the generic type or method 'D.Interceptor1<T>(T)'
            //     [InterceptsLocation(1, "jdOMgqJQrFcHcRZ6LGTup74AAAA=")] // 1
            Diagnostic(ErrorCode.ERR_ValConstraintNotSatisfied, "InterceptsLocation").WithArguments("D.Interceptor1<T>(T)", "T", "string").WithLocation(5, 6));
    }

    [Fact]
    public void InterceptableGeneric_07()
    {
        // original containing type is generic
        var source = """
            D.Usage(1);
            D.Usage(2);

            class C<T1>
            {
                public static void InterceptableMethod(T1 t) => throw null!;
            }

            static partial class D
            {
                public static void Usage<T2>(T2 t)
                {
                    C<T2>.InterceptableMethod(t);
                    C<object>.InterceptableMethod(t);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                public static void Interceptor1<T>(T t) { Console.Write(t); }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1122");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_09()
    {
        // original containing type and method are generic
        // interceptor has arity 2
        var source = """
            D.Usage(1, "a");
            D.Usage(2, "b");

            class C<T1>
            {
                public static void InterceptableMethod<T2>(T1 t1, T2 t2) => throw null!;
            }

            static partial class D
            {
                public static void Usage<T1, T2>(T1 t1, T2 t2)
                {
                    C<T1>.InterceptableMethod(t1, t2);
                    C<object>.InterceptableMethod<object>(t1, t2);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                public static void Interceptor1<T1, T2>(T1 t1, T2 t2)
                {
                    Console.Write(t1);
                    Console.Write(t2);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1a1a2b2b");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_10()
    {
        // original containing type and method are generic
        // interceptor has arity 1

        // Note: the behavior in this scenario might push us toward using a "unification" model for generic interceptors.
        // All the cases supported in our current design would also be supported by unification, so we should be able to add it later.
        var source = """

            class C<T1>
            {
                public static void InterceptableMethod<T2>(T1 t1, T2 t2) => throw null!;
            }

            static partial class D
            {
                public static void Usage<T>(object obj, T t)
                {
                    C<object>.InterceptableMethod(obj, t);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static void Interceptor1<T>(object obj, T t) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9177: Method 'D.Interceptor1<T>(object, T)' must be non-generic or have arity 2 to match 'C<object>.InterceptableMethod<T>(object, T)'.
            //     [InterceptsLocation(1, "h+iqjaw4yol1Ge3U77MWn8sAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorArityNotCompatible, "InterceptsLocation").WithArguments("D.Interceptor1<T>(object, T)", "2", "C<object>.InterceptableMethod<T>(object, T)").WithLocation(5, 6));
    }

    [Fact]
    public void InterceptableGeneric_11()
    {
        // original containing type and method are generic
        // interceptor has arity 0
        var source = """
            class C<T1>
            {
                public static void InterceptableMethod<T2>(T1 t1, T2 t2) => throw null!;
            }

            static partial class D
            {
                public static void Main()
                {
                    C<int>.InterceptableMethod(1, "a");
                    C<int>.InterceptableMethod<string>(2, "b");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1(int i, string s)
                {
                    Console.Write(i);
                    Console.Write(s);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1a2b");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_12()
    {
        // original grandparent type and method are generic
        // interceptor has arity 2
        var source = """
            D.Usage(1, "a");
            D.Usage(2, "b");

            class Outer<T1>
            {
                public class C
                {
                    public static void InterceptableMethod<T2>(T1 t1, T2 t2) => throw null!;
                }
            }

            static partial class D
            {
                public static void Usage<T1, T2>(T1 t1, T2 t2)
                {
                    Outer<T1>.C.InterceptableMethod(t1, t2);
                    Outer<object>.C.InterceptableMethod<object>(t1, t2);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                public static void Interceptor1<T1, T2>(T1 t1, T2 t2)
                {
                    Console.Write(t1);
                    Console.Write(t2);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1a1a2b2b");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_13()
    {
        // original grandparent type, containing type, and method are generic
        // interceptor has arity 3
        var source = """
            D.Usage(1, 2, 3);
            D.Usage(4, 5, 6);

            class Outer<T1>
            {
                public class C<T2>
                {
                    public static void InterceptableMethod<T3>(T1 t1, T2 t2, T3 t3) => throw null!;
                }
            }

            static partial class D
            {
                public static void Usage<T1, T2, T3>(T1 t1, T2 t2, T3 t3)
                {
                    Outer<T1>.C<T2>.InterceptableMethod(t1, t2, t3);
                    Outer<object>.C<object>.InterceptableMethod<object>(t1, t2, t3);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                public static void Interceptor1<T1, T2, T3>(T1 t1, T2 t2, T3 t3)
                {
                    Console.Write(t1);
                    Console.Write(t2);
                    Console.Write(t3);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "123123456456");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_14()
    {
        // containing type has 2 type parameters, method is generic
        // interceptor has arity 3
        var source = """
            D.Usage(1, 2, 3);
            D.Usage(4, 5, 6);

            class C<T1, T2>
            {
                public static void InterceptableMethod<T3>(T1 t1, T2 t2, T3 t3) => throw null!;
            }

            static partial class D
            {
                public static void Usage<T1, T2, T3>(T1 t1, T2 t2, T3 t3)
                {
                    C<T1, T2>.InterceptableMethod(t1, t2, t3);
                    C<object, object>.InterceptableMethod<object>(t1, t2, t3);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static partial class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                public static void Interceptor1<T1, T2, T3>(T1 t1, T2 t2, T3 t3)
                {
                    Console.Write(t1);
                    Console.Write(t2);
                    Console.Write(t3);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "123123456456");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptableGeneric_15()
    {
        // original method is non-generic, interceptor is generic
        var source = """
            C.Original();

            class C
            {
                public static void Original() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static void Interceptor1<T>() => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9178: Method 'D.Interceptor1<T>()' must be non-generic to match 'C.Original()'.
            //     [InterceptsLocation(1, "PfujbqHNZVPxez1Ug8rXIAIAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorCannotBeGeneric, "InterceptsLocation").WithArguments("D.Interceptor1<T>()", "C.Original()").WithLocation(5, 6));
    }

    [Fact]
    public void InterceptableGeneric_16()
    {
        var source = """
            #nullable enable

            class C
            {
                public static void InterceptableMethod<T1>(T1 t) => throw null!;

                public static void Main()
                {
                    C.InterceptableMethod<string?>(null);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            #nullable enable

            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static void Interceptor1<T>(T t) where T : notnull => Console.Write(1);
            }
            """;
        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // (8,6): warning CS8714: The type 'string?' cannot be used as type parameter 'T' in the generic type or method 'D.Interceptor1<T>(T)'. Nullability of type argument 'string?' doesn't match 'notnull' constraint.
            //     [InterceptsLocation(1, "kWQj9SspA3ZDLFRLuB00A5gAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInTypeParameterNotNullConstraint, "InterceptsLocation").WithArguments("D.Interceptor1<T>(T)", "T", "string?").WithLocation(8, 6));
    }

    [Fact]
    public void InterceptsLocationBadAttributeArguments_01()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation("Program.cs", 1, "10")]
                [InterceptsLocation("Program.cs", 1, 1, 9999)]
                [InterceptsLocation("Program.cs", ERROR, 1)]
                [InterceptsLocation()]
                public static void Interceptor1(string param) { Console.Write("interceptor " + param); }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(6,42): error CS1503: Argument 3: cannot convert from 'string' to 'int'
            //     [InterceptsLocation("Program.cs", 1, "10")]
            Diagnostic(ErrorCode.ERR_BadArgType, @"""10""").WithArguments("3", "string", "int").WithLocation(6, 42),
            // Program.cs(7,6): error CS1729: 'InterceptsLocationAttribute' does not contain a constructor that takes 4 arguments
            //     [InterceptsLocation("Program.cs", 1, 1, 9999)]
            Diagnostic(ErrorCode.ERR_BadCtorArgCount, @"InterceptsLocation(""Program.cs"", 1, 1, 9999)").WithArguments("System.Runtime.CompilerServices.InterceptsLocationAttribute", "4").WithLocation(7, 6),
            // Program.cs(8,39): error CS0103: The name 'ERROR' does not exist in the current context
            //     [InterceptsLocation("Program.cs", ERROR, 1)]
            Diagnostic(ErrorCode.ERR_NameNotInContext, "ERROR").WithArguments("ERROR").WithLocation(8, 39),
            // Program.cs(9,6): error CS1729: 'InterceptsLocationAttribute' does not contain a constructor that takes 0 arguments
            //     [InterceptsLocation()]
            Diagnostic(ErrorCode.ERR_BadCtorArgCount, "InterceptsLocation()").WithArguments("System.Runtime.CompilerServices.InterceptsLocationAttribute", "0").WithLocation(9, 6)
            );
    }

    [Fact]
    public void InterceptsLocationBadPath_01()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("BAD", 15, 11)]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("BAD", 15, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""BAD"", 15, 11)").WithLocation(21, 6),
            // Program.cs(21,25): error CS9139: Cannot intercept: compilation does not contain a file with path 'BAD'.
            //     [InterceptsLocation("BAD", 15, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""BAD""").WithArguments("BAD").WithLocation(21, 25)
            );
    }

    [Fact]
    public void InterceptsLocationBadPath_02()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("projects/Program.cs", 15, 11)]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, PlatformInformation.IsWindows ? @"C:\Users\me\projects\Program.cs" : "/Users/me/projects/Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // C:\Users\me\projects\Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("projects/Program.cs", 15, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""projects/Program.cs"", 15, 11)").WithLocation(21, 6),
            // C:\Users\me\projects\Program.cs(21,25): error CS9140: Cannot intercept: compilation does not contain a file with path 'projects/Program.cs'. Did you mean to use path 'Program.cs'?
            //     [InterceptsLocation("projects/Program.cs", 15, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilationWithCandidate, @"""projects/Program.cs""").WithArguments("projects/Program.cs", "Program.cs").WithLocation(21, 25)
            );
    }

    [Fact]
    public void InterceptsLocationBadPath_03()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C { }

            static class Program
            {

                public static C InterceptableMethod(this C c, string param) { Console.Write("interceptable " + param); return c; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation(null, 15, 11)]
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(20,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(null, 15, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, "InterceptsLocation(null, 15, 11)").WithLocation(20, 6),
            // Program.cs(20,25): error CS9150: Interceptor cannot have a 'null' file path.
            //     [InterceptsLocation(null, 15, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorFilePathCannotBeNull, "null").WithLocation(20, 25)
            );
    }

    [Fact]
    public void InterceptsLocationBadPath_04()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C { }

            static class Program
            {

                public static C InterceptableMethod(this C c, string param) { Console.Write("interceptable " + param); return c; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("program.cs", 15, 11)]
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(20,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("program.cs", 15, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""program.cs"", 15, 11)").WithLocation(20, 6),
            // Program.cs(20,25): error CS9139: Cannot intercept: compilation does not contain a file with path 'program.cs'.
            //     [InterceptsLocation("program.cs", 15, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""program.cs""").WithArguments("program.cs").WithLocation(20, 25)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_01()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 25, 1)]
                [InterceptsLocation("Program.cs", 26, 1)]
                [InterceptsLocation("Program.cs", 100, 1)]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 25, 1)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 25, 1)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token '}'.
            //     [InterceptsLocation("Program.cs", 25, 1)]
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 25, 1)").WithArguments("}").WithLocation(21, 6),
            // Program.cs(22,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 26, 1)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 26, 1)").WithLocation(22, 6),
            // Program.cs(22,39): error CS9142: The given file has '25' lines, which is fewer than the provided line number '26'.
            //     [InterceptsLocation("Program.cs", 26, 1)]
            Diagnostic(ErrorCode.ERR_InterceptorLineOutOfRange, "26").WithArguments("25", "26").WithLocation(22, 39),
            // Program.cs(23,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 100, 1)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 100, 1)").WithLocation(23, 6),
            // Program.cs(23,39): error CS9142: The given file has '25' lines, which is fewer than the provided line number '100'.
            //     [InterceptsLocation("Program.cs", 100, 1)]
            Diagnostic(ErrorCode.ERR_InterceptorLineOutOfRange, "100").WithArguments("25", "100").WithLocation(23, 39)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_02()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 16, 5)]
                [InterceptsLocation("Program.cs", 16, 6)]
                [InterceptsLocation("Program.cs", 16, 1000)]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 16, 5)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 16, 5)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token '}'.
            //     [InterceptsLocation("Program.cs", 16, 5)]
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 16, 5)").WithArguments("}").WithLocation(21, 6),
            // Program.cs(22,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 16, 6)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 16, 6)").WithLocation(22, 6),
            // Program.cs(22,43): error CS9143: The given line is '5' characters long, which is fewer than the provided character number '6'.
            //     [InterceptsLocation("Program.cs", 16, 6)]
            Diagnostic(ErrorCode.ERR_InterceptorCharacterOutOfRange, "6").WithArguments("5", "6").WithLocation(22, 43),
            // Program.cs(23,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 16, 1000)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 16, 1000)").WithLocation(23, 6),
            // Program.cs(23,43): error CS9143: The given line is '5' characters long, which is fewer than the provided character number '1000'.
            //     [InterceptsLocation("Program.cs", 16, 1000)]
            Diagnostic(ErrorCode.ERR_InterceptorCharacterOutOfRange, "1000").WithArguments("5", "1000").WithLocation(23, 43)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_03()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 15, 9)]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 15, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 15, 9)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9141: The provided line and character number does not refer to an interceptable method name, but rather to token 'c'.
            //     [InterceptsLocation("Program.cs", 15, 9)]
            Diagnostic(ErrorCode.ERR_InterceptorPositionBadToken, @"InterceptsLocation(""Program.cs"", 15, 9)").WithArguments("c").WithLocation(21, 6)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_04()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }

            static class D
            {
                [InterceptsLocation("Program.cs", 15, 13)]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 15, 13)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 15, 13)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9147: The provided line and character number does not refer to the start of token 'InterceptableMethod'. Did you mean to use line '15' and character '11'?
            //     [InterceptsLocation("Program.cs", 15, 13)]
            Diagnostic(ErrorCode.ERR_InterceptorMustReferToStartOfTokenPosition, @"InterceptsLocation(""Program.cs"", 15, 13)").WithArguments("InterceptableMethod", "15", "11").WithLocation(21, 6)
        );
    }

    [Fact]
    public void InterceptsLocationBadPosition_05()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C { }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.
                        InterceptableMethod("call site");

                    c.InterceptableMethod    ("call site");
                }


                public static C InterceptableMethod(this C c, string param) { Console.Write("interceptable " + param); return c; }

                [InterceptsLocation("Program.cs", 12, 11)] // intercept spaces before 'InterceptableMethod' token
                [InterceptsLocation("Program.cs", 14, 33)] // intercept spaces after 'InterceptableMethod' token
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }

            static class CExt
            {
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(20,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 12, 11)] // intercept spaces before 'InterceptableMethod' token
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 12, 11)").WithLocation(20, 6),
            // Program.cs(20,6): error CS9147: The provided line and character number does not refer to the start of token 'InterceptableMethod'. Did you mean to use line '12' and character '13'?
            //     [InterceptsLocation("Program.cs", 12, 11)] // intercept spaces before 'InterceptableMethod' token
            Diagnostic(ErrorCode.ERR_InterceptorMustReferToStartOfTokenPosition, @"InterceptsLocation(""Program.cs"", 12, 11)").WithArguments("InterceptableMethod", "12", "13").WithLocation(20, 6),
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 14, 33)] // intercept spaces after 'InterceptableMethod' token
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 14, 33)").WithLocation(21, 6),
            // Program.cs(21,6): error CS9147: The provided line and character number does not refer to the start of token 'InterceptableMethod'. Did you mean to use line '14' and character '11'?
            //     [InterceptsLocation("Program.cs", 14, 33)] // intercept spaces after 'InterceptableMethod' token
            Diagnostic(ErrorCode.ERR_InterceptorMustReferToStartOfTokenPosition, @"InterceptsLocation(""Program.cs"", 14, 33)").WithArguments("InterceptableMethod", "14", "11").WithLocation(21, 6)
        );
    }

    [Fact]
    public void InterceptsLocationBadPosition_06()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C { }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod/*comment*/("call site");
                }


                public static C InterceptableMethod(this C c, string param) { Console.Write("interceptable " + param); return c; }

                [InterceptsLocation("Program.cs", 11, 31)] // intercept comment after 'InterceptableMethod' token
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }

            static class CExt
            {
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(17,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 11, 31)] // intercept comment after 'InterceptableMethod' token
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 11, 31)").WithLocation(17, 6),
            // Program.cs(17,6): error CS9147: The provided line and character number does not refer to the start of token 'InterceptableMethod'. Did you mean to use line '11' and character '11'?
            //     [InterceptsLocation("Program.cs", 11, 31)] // intercept comment after 'InterceptableMethod' token
            Diagnostic(ErrorCode.ERR_InterceptorMustReferToStartOfTokenPosition, @"InterceptsLocation(""Program.cs"", 11, 31)").WithArguments("InterceptableMethod", "11", "11").WithLocation(17, 6)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_07()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C { }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.
                        // comment
                        InterceptableMethod("call site");
                }


                public static C InterceptableMethod(this C c, string param) { Console.Write("interceptable " + param); return c; }

                [InterceptsLocation("Program.cs", 12, 13)] // intercept comment above 'InterceptableMethod' token
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }

            static class CExt
            {
            }
            """;

        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(19,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 12, 13)] // intercept comment above 'InterceptableMethod' token
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 12, 13)").WithLocation(19, 6),
            // Program.cs(19,6): error CS9147: The provided line and character number does not refer to the start of token 'InterceptableMethod'. Did you mean to use line '13' and character '13'?
            //     [InterceptsLocation("Program.cs", 12, 13)] // intercept comment above 'InterceptableMethod' token
            Diagnostic(ErrorCode.ERR_InterceptorMustReferToStartOfTokenPosition, @"InterceptsLocation(""Program.cs"", 12, 13)").WithArguments("InterceptableMethod", "13", "13").WithLocation(19, 6)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_08()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C { }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }


                public static C InterceptableMethod(this C c, string param) { Console.Write("interceptable " + param); return c; }

                [InterceptsLocation("Program.cs", -1, 1)] // 1
                [InterceptsLocation("Program.cs", 1, -1)] // 2
                [InterceptsLocation("Program.cs", -1, -1)] // 3
                [InterceptsLocation("Program.cs", 0, 1)] // 4
                [InterceptsLocation("Program.cs", 1, 0)] // 5 
                [InterceptsLocation("Program.cs", 0, 0)] // 6
                public static C Interceptor1(this C c, string param) { Console.Write("interceptor " + param); return c; }
            }
            """;

        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(17,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", -1, 1)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", -1, 1)").WithLocation(17, 6),
            // Program.cs(17,39): error CS9157: Line and character numbers provided to InterceptsLocationAttribute must be positive.
            //     [InterceptsLocation("Program.cs", -1, 1)] // 1
            Diagnostic(ErrorCode.ERR_InterceptorLineCharacterMustBePositive, "-1").WithLocation(17, 39),
            // Program.cs(18,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 1, -1)] // 2
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 1, -1)").WithLocation(18, 6),
            // Program.cs(18,42): error CS9157: Line and character numbers provided to InterceptsLocationAttribute must be positive.
            //     [InterceptsLocation("Program.cs", 1, -1)] // 2
            Diagnostic(ErrorCode.ERR_InterceptorLineCharacterMustBePositive, "-1").WithLocation(18, 42),
            // Program.cs(19,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", -1, -1)] // 3
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", -1, -1)").WithLocation(19, 6),
            // Program.cs(19,39): error CS9157: Line and character numbers provided to InterceptsLocationAttribute must be positive.
            //     [InterceptsLocation("Program.cs", -1, -1)] // 3
            Diagnostic(ErrorCode.ERR_InterceptorLineCharacterMustBePositive, "-1").WithLocation(19, 39),
            // Program.cs(20,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 0, 1)] // 4
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 0, 1)").WithLocation(20, 6),
            // Program.cs(20,39): error CS9157: Line and character numbers provided to InterceptsLocationAttribute must be positive.
            //     [InterceptsLocation("Program.cs", 0, 1)] // 4
            Diagnostic(ErrorCode.ERR_InterceptorLineCharacterMustBePositive, "0").WithLocation(20, 39),
            // Program.cs(21,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 1, 0)] // 5 
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 1, 0)").WithLocation(21, 6),
            // Program.cs(21,42): error CS9157: Line and character numbers provided to InterceptsLocationAttribute must be positive.
            //     [InterceptsLocation("Program.cs", 1, 0)] // 5 
            Diagnostic(ErrorCode.ERR_InterceptorLineCharacterMustBePositive, "0").WithLocation(21, 42),
            // Program.cs(22,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 0, 0)] // 6
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 0, 0)").WithLocation(22, 6),
            // Program.cs(22,39): error CS9157: Line and character numbers provided to InterceptsLocationAttribute must be positive.
            //     [InterceptsLocation("Program.cs", 0, 0)] // 6
            Diagnostic(ErrorCode.ERR_InterceptorLineCharacterMustBePositive, "0").WithLocation(22, 39)
            );
    }

    [Fact]
    public void InterceptsLocationBadPosition_Checksum_01()
    {
        var sourceTree = CSharpTestSource.Parse("""
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """, options: RegularWithInterceptors);

        // test unexpected position within interceptable name token
        var interceptableName = sourceTree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last().GetInterceptableNameSyntax()!;
        var position = interceptableName.Position + 1;

        var builder = new BlobBuilder();
        builder.WriteBytes(sourceTree.GetText().GetContentHash());
        builder.WriteInt32(position);
        builder.WriteUTF8("Error");

        var base64 = Convert.ToBase64String(builder.ToArray());

        var interceptorTree = CSharpTestSource.Parse($$"""
            using System.Runtime.CompilerServices;
            using System;
            
            static class D
            {
                [InterceptsLocation(1, "{{base64}}")]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """, options: RegularWithInterceptors);
        var comp = CreateCompilation([sourceTree, interceptorTree, s_attributesTree]);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9235: The data argument to InterceptsLocationAttribute refers to an invalid position in file 'Error'.
            //     [InterceptsLocation(1, "ExWKMussA+NMlN5J0QNXiEMBAABFcnJvcg==")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidPosition, "InterceptsLocation").WithArguments("Error").WithLocation(6, 6)
        );
    }

    [Theory]
    [InlineData(-1)] // test invalid position
    [InlineData(99999)] // test position past end of the file
    public void InterceptsLocationBadPosition_Checksum_02(int position)
    {
        var sourceTree = CSharpTestSource.Parse("""
            using System.Runtime.CompilerServices;
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """, options: RegularWithInterceptors);

        var builder = new BlobBuilder();
        builder.WriteBytes(sourceTree.GetText().GetContentHash());
        builder.WriteInt32(position);
        builder.WriteUTF8("Error");

        var base64 = Convert.ToBase64String(builder.ToArray());

        var interceptorTree = CSharpTestSource.Parse($$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation(1, "{{base64}}")]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """, options: RegularWithInterceptors);
        var comp = CreateCompilation([sourceTree, interceptorTree, s_attributesTree]);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9235: The data argument to InterceptsLocationAttribute refers to an invalid position in file 'Error'.
            //     [InterceptsLocation(1, "ExWKMussA+NMlN5J0QNXiJ+GAQBFcnJvcg==")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidPosition, "InterceptsLocation").WithArguments("Error").WithLocation(6, 6)
        );
    }

    [Fact]
    public void SignatureMismatch_01()
    {
        var source = """
            using System;

            interface I1 { }
            class C : I1 { }

            static class Program
            {

                public static I1 InterceptableMethod(this I1 i1, string param) { Console.Write("interceptable " + param); return i1; }

                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static I1 Interceptor1(this I1 i1, int param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9144: Cannot intercept method 'Program.InterceptableMethod(I1, string)' with interceptor 'D.Interceptor1(I1, int)' because the signatures do not match.
            //     [InterceptsLocation(1, "OkC1VTxf7+rwBoJPC2IWBhoBAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("Program.InterceptableMethod(I1, string)", "D.Interceptor1(I1, int)").WithLocation(6, 6)
            );
    }

    [Fact]
    public void SignatureMismatch_02()
    {
        // Instance method receiver type differs from interceptor 'this' parameter type.
        var source = """
            using System;

            interface I1 { }
            class C : I1
            {

                public I1 InterceptableMethod(string param) { Console.Write("interceptable " + param); return this; }
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static I1 Interceptor1(this I1 i1, string param) { Console.Write("interceptor " + param); return i1; }
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'C this' on 'C.InterceptableMethod(string)'.
            //     [InterceptsLocation(1, "ASfq/xnhlb1QGHJAQ5lqJQkBAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("C this", "C.InterceptableMethod(string)").WithLocation(6, 6)
            );
    }

    [Fact]
    public void SignatureMismatch_03()
    {
        // Instance method 'this' parameter ref kind differs from interceptor 'this' parameter ref kind.
        var source = """
            using System;

            struct S
            {

                public void InterceptableMethod(string param) { Console.Write("interceptable " + param); }
            }

            static class Program
            {
                public static void Main()
                {
                    var s = new S();
                    s.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1(this S s, string param) { Console.Write("interceptor " + param); }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'ref S this' on 'S.InterceptableMethod(string)'.
            //     [InterceptsLocation(1, "fqj37DJySjNYT6e7owxQrugAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("ref S this", "S.InterceptableMethod(string)").WithLocation(6, 6)
            );
    }

    [Fact]
    public void SignatureMismatch_04()
    {
        // Safe nullability difference
        var source = """
            class C
            {

                public string? InterceptableMethod(string param) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.InterceptableMethod("call site");
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static string Interceptor1(this C s, string? param) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, options: WithNullableEnable());
        comp.VerifyEmitDiagnostics();
    }

    [Fact]
    public void SignatureMismatch_05()
    {
        // Unsafe nullability difference
        var source = """
            class C
            {

                public void Method1(string? param1) => throw null!;


                public string Method2() => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.Method1("call site");
                    _ = c.Method2();
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static void Interceptor1(this C s, string param2) => throw null!;

                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 2
                public static string? Interceptor2(this C s) => throw null!;
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, options: WithNullableEnable());
        comp.VerifyEmitDiagnostics(
            // (5,6): warning CS9159: Nullability of reference types in type of parameter 'param2' doesn't match interceptable method 'C.Method1(string?)'.
            //     [InterceptsLocation(1, "535qo9hAHI56AdJiXvHKiOAAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInParameterTypeOnInterceptor, "InterceptsLocation").WithArguments("param2", "C.Method1(string?)").WithLocation(5, 6),
            // (8,6): warning CS9158: Nullability of reference types in return type doesn't match interceptable method 'C.Method2()'.
            //     [InterceptsLocation(1, "535qo9hAHI56AdJiXvHKiAUBAAA=")] // 2
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInReturnTypeOnInterceptor, "InterceptsLocation").WithArguments("C.Method2()").WithLocation(8, 6)
            );

        comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, options: WithNullableDisable());
        comp.VerifyEmitDiagnostics(
            // (4,31): warning CS8632: The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
            //     public void Method1(string? param1) => throw null!;
            Diagnostic(ErrorCode.WRN_MissingNonNullTypesContextForAnnotation, "?").WithLocation(4, 31),
            // (9,25): warning CS8632: The annotation for nullable reference types should only be used in code within a '#nullable' annotations context.
            //     public static string? Interceptor2(this C s) => throw null!;
            Diagnostic(ErrorCode.WRN_MissingNonNullTypesContextForAnnotation, "?").WithLocation(9, 25)
            );
    }

    [Fact]
    public void SignatureMismatch_06()
    {
        // 'dynamic' difference
        var source = """
            class C
            {

                public void Method1(object param1) => throw null!;


                public dynamic Method2() => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.Method1("call site");
                    _ = c.Method2();
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static void Interceptor1(this C s, dynamic param2) => throw null!;

                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 2
                public static object Interceptor2(this C s) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): warning CS9154: Intercepting a call to 'C.Method1(object)' with interceptor 'D.Interceptor1(C, dynamic)', but the signatures do not match.
            //     [InterceptsLocation(1, "tat2uM+CawVuszRsZdGgpOAAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method1(object)", "D.Interceptor1(C, dynamic)").WithLocation(5, 6),
            // (8,6): warning CS9154: Intercepting a call to 'C.Method2()' with interceptor 'D.Interceptor2(C)', but the signatures do not match.
            //     [InterceptsLocation(1, "tat2uM+CawVuszRsZdGgpAUBAAA=")] // 2
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method2()", "D.Interceptor2(C)").WithLocation(8, 6)
            );
    }

    [Fact]
    public void SignatureMismatch_07()
    {
        // tuple element name difference
        var source = """
            class C
            {
                public void Method1((string a, string b) param1) => throw null!;
                public void Method2((string x, string y) param1) => throw null!;
                public void Method3((string, string) param1) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();

                    c.Method1(default!);
                    c.Method2(default!);
                    c.Method3(default!);

                    c.Method1(default!);
                    c.Method2(default!);
                    c.Method3(default!);

                    c.Method1(default!);
                    c.Method2(default!);
                    c.Method3(default!);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 1
                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})] // 2
                public static void Interceptor1(this C s, (string a, string b) param2) => Console.Write(1);

                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})] // 3
                [InterceptsLocation({{GetAttributeArgs(locations[4]!)}})]
                [InterceptsLocation({{GetAttributeArgs(locations[5]!)}})] // 4
                public static void Interceptor2(this C s, (string x, string y) param2) => Console.Write(2);

                [InterceptsLocation({{GetAttributeArgs(locations[6]!)}})] // 5
                [InterceptsLocation({{GetAttributeArgs(locations[7]!)}})] // 6
                [InterceptsLocation({{GetAttributeArgs(locations[8]!)}})]
                public static void Interceptor3(this C s, (string, string) param2) => Console.Write(3);
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "111222333");
        verifier.VerifyDiagnostics(
            // (7,6): warning CS9154: Intercepting a call to 'C.Method2((string x, string y))' with interceptor 'D.Interceptor1(C, (string a, string b))', but the signatures do not match.
            //     [InterceptsLocation(1, "hiVsFn3lfjYE43RKvMvwPGIBAAA=")] // 1
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method2((string x, string y))", "D.Interceptor1(C, (string a, string b))").WithLocation(7, 6),
            // (8,6): warning CS9154: Intercepting a call to 'C.Method3((string, string))' with interceptor 'D.Interceptor1(C, (string a, string b))', but the signatures do not match.
            //     [InterceptsLocation(1, "hiVsFn3lfjYE43RKvMvwPIABAAA=")] // 2
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method3((string, string))", "D.Interceptor1(C, (string a, string b))").WithLocation(8, 6),
            // (11,6): warning CS9154: Intercepting a call to 'C.Method1((string a, string b))' with interceptor 'D.Interceptor2(C, (string x, string y))', but the signatures do not match.
            //     [InterceptsLocation(1, "hiVsFn3lfjYE43RKvMvwPKABAAA=")] // 3
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method1((string a, string b))", "D.Interceptor2(C, (string x, string y))").WithLocation(11, 6),
            // (13,6): warning CS9154: Intercepting a call to 'C.Method3((string, string))' with interceptor 'D.Interceptor2(C, (string x, string y))', but the signatures do not match.
            //     [InterceptsLocation(1, "hiVsFn3lfjYE43RKvMvwPNwBAAA=")] // 4
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method3((string, string))", "D.Interceptor2(C, (string x, string y))").WithLocation(13, 6),
            // (16,6): warning CS9154: Intercepting a call to 'C.Method1((string a, string b))' with interceptor 'D.Interceptor3(C, (string, string))', but the signatures do not match.
            //     [InterceptsLocation(1, "hiVsFn3lfjYE43RKvMvwPPwBAAA=")] // 5
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method1((string a, string b))", "D.Interceptor3(C, (string, string))").WithLocation(16, 6),
            // (17,6): warning CS9154: Intercepting a call to 'C.Method2((string x, string y))' with interceptor 'D.Interceptor3(C, (string, string))', but the signatures do not match.
            //     [InterceptsLocation(1, "hiVsFn3lfjYE43RKvMvwPBoCAAA=")] // 6
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method2((string x, string y))", "D.Interceptor3(C, (string, string))").WithLocation(17, 6)
            );
    }

    [Fact]
    public void SignatureMismatch_08()
    {
        // nint/IntPtr difference
        var source = """
            using System;

            class C
            {

                public void Method1(nint param1) => throw null!;
                public void Method2(IntPtr param1) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    c.Method1(default!);
                    c.Method2(default!);

                    c.Method2(default!);
                    c.Method1(default!);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor1(this C s, IntPtr param2) => Console.Write(1);

                [InterceptsLocation({{GetAttributeArgs(locations[2]!)}})] // 2
                [InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
                public static void Interceptor2(this C s, nint param2) => Console.Write(2);
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1122");
        verifier.VerifyDiagnostics(
            // (6,6): warning CS9154: Intercepting a call to 'C.Method1(nint)' with interceptor 'D.Interceptor1(C, IntPtr)', but the signatures do not match.
            //     [InterceptsLocation(1, "3ONc0QK7vNwCujpTORn5YvcAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method1(nint)", "D.Interceptor1(C, System.IntPtr)").WithLocation(6, 6),
            // (10,6): warning CS9154: Intercepting a call to 'C.Method2(IntPtr)' with interceptor 'D.Interceptor2(C, nint)', but the signatures do not match.
            //     [InterceptsLocation(1, "3ONc0QK7vNwCujpTORn5YjUBAAA=")] // 2
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C.Method2(System.IntPtr)", "D.Interceptor2(C, nint)").WithLocation(10, 6));
    }

    [Fact]
    public void SignatureMismatch_09()
    {
        var source = """
            using System;

            static class Program
            {
                public static void InterceptableMethod(ref readonly int x) => Console.Write("interceptable " + x);

                public static void Main()
                {
                    int x = 5;
                    InterceptableMethod(in x);
                }
            }
            """;

        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor(in int x) => Console.Write("interceptor " + x);
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9144: Cannot intercept method 'Console.Write(string)' with interceptor 'D.Interceptor(in int)' because the signatures do not match.
            //     [InterceptsLocation(1, "tMK4g8K+v1dr3MEydPY1wXQAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("System.Console.Write(string)", "D.Interceptor(in int)").WithLocation(6, 6));
    }

    [Fact]
    public void SignatureMismatch_10()
    {
        var source = """
            using System;

            struct Program
            {
                public void InterceptableMethod() => Console.Write("Original");

                public static void Main()
                {
                    new Program().InterceptableMethod();
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor(this in Program x) => Console.Write("Intercepted");
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'ref Program this' on 'Program.InterceptableMethod()'.
            //     [InterceptsLocation(1, "g6EgVyqlLSG1DCYza3Uon6cAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("ref Program this", "Program.InterceptableMethod()").WithLocation(6, 6));
    }

    [Fact]
    public void SignatureMismatch_11()
    {
        var source = ("""
            using System;

            struct Program
            {
                public readonly void InterceptableMethod() => Console.Write("Original");

                public static void Main()
                {
                    new Program().InterceptableMethod();
                }
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor(this in Program x) => Console.Write("Intercepted");
            }
            """, "Interceptor.cs");
        var verifier = CompileAndVerify(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Original");
        verifier.VerifyDiagnostics();

        verifier = CompileAndVerify(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Intercepted");
        verifier.VerifyDiagnostics();
    }

    [Theory]
    [InlineData("ref readonly")]
    [InlineData("ref")]
    [WorkItem("https://github.com/dotnet/roslyn/issues/71714")]
    public void SignatureMismatch_12(string interceptorRefKind)
    {
        var source = ("""
            using System;

            struct Program
            {
                public readonly void InterceptableMethod() => Console.Write("Original");

                public static void Main()
                {
                    new Program().InterceptableMethod();
                }
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor(this {{interceptorRefKind}} Program x) => Console.Write("Intercepted");
            }
            """, "Interceptor.cs");
        var verifier = CompileAndVerify(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Original");
        verifier.VerifyDiagnostics();

        // 'this ref readonly' should probably be compatible with 'readonly' original method.
        // Tracked by https://github.com/dotnet/roslyn/issues/71714
        var comp = CreateCompilation(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(6,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'in Program this' on 'Program.InterceptableMethod()'.
            //     [InterceptsLocation(1, "Z8jOxZ1RAOmFFHIDd0PvFLAAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "InterceptsLocation").WithArguments("in Program this", "Program.InterceptableMethod()").WithLocation(6, 6));
    }

    [Fact]
    public void ScopedMismatch_01()
    {
        // Unsafe 'scoped' difference
        var source = """
            class C
            {

                public static ref int InterceptableMethod(scoped ref int value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    int i = 0;
                    C.InterceptableMethod(ref i);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static ref int Interceptor1(ref int value) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, options: WithNullableEnable());
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9156: Cannot intercept call to 'C.InterceptableMethod(scoped ref int)' with 'D.Interceptor1(ref int)' because of a difference in 'scoped' modifiers or '[UnscopedRef]' attributes.
            //     [InterceptsLocation(1, "0iiDkFPlvM/mJGTV+4iUXcUAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorScopedMismatch, "InterceptsLocation").WithArguments("C.InterceptableMethod(scoped ref int)", "D.Interceptor1(ref int)").WithLocation(5, 6)
            );
    }

    [Fact]
    public void ScopedMismatch_02()
    {
        // safe 'scoped' difference
        var source = """
            class C
            {

                public static ref int InterceptableMethod(ref int value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    int i = 0;
                    _ = C.InterceptableMethod(ref i);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                static int i;

                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static ref int Interceptor1(scoped ref int value)
                {
                    Console.Write(1);
                    return ref i;
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void ScopedMismatch_03()
    {
        // safe '[UnscopedRef]' difference
        var source = """
            using System.Diagnostics.CodeAnalysis;

            class C
            {

                public static ref int InterceptableMethod([UnscopedRef] out int value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    _ = C.InterceptableMethod(out int i);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                static int i;

                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static ref int Interceptor1(out int value)
                {
                    Console.Write(1);
                    value = 0;
                    return ref i;
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptor, s_attributesSource, UnscopedRefAttributeDefinition], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void ScopedMismatch_04()
    {
        // unsafe '[UnscopedRef]' difference
        var source = """
            class C
            {

                public static ref int InterceptableMethod(out int value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod(out int i);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Diagnostics.CodeAnalysis;
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
                public static ref int Interceptor1([UnscopedRef] out int value) => throw null!;
            }
            """;
        var comp = CreateCompilation([source, interceptor, s_attributesSource, UnscopedRefAttributeDefinition], parseOptions: RegularWithInterceptors, options: WithNullableEnable());
        comp.VerifyEmitDiagnostics(
            // (7,6): error CS9156: Cannot intercept call to 'C.InterceptableMethod(out int)' with 'D.Interceptor1(out int)' because of a difference in 'scoped' modifiers or '[UnscopedRef]' attributes.
            //     [InterceptsLocation(1, "vduOaI3RVsjD7fczcgX/N6oAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorScopedMismatch, "InterceptsLocation").WithArguments("C.InterceptableMethod(out int)", "D.Interceptor1(out int)").WithLocation(7, 6)
            );
    }

    [Fact]
    public void ReferenceEquals_01()
    {
        // A call to 'object.ReferenceEquals(a, b)' is defined as being equivalent to '(object)a == b'.
        var source = """
            using System.Runtime.CompilerServices;

            static class D
            {

                public static bool Interceptable(object? obj1, object? obj2) => throw null!;

                public static void M0(object? obj1, object? obj2)
                {
                    if (obj1 == obj2)
                       throw null!;
                }

                public static void M1(object? obj1, object? obj2)
                {
                    if (Interceptable(obj1, obj2))
                       throw null!;
                }

                public static void M2(object? obj1, object? obj2)
                {
                    if (Interceptable(obj1, obj2))
                       throw null!;
                }
            }

            namespace System
            {
                public class Object
                {
                    [InterceptsLocation("Program.cs", 16, 13)]
                    public static bool ReferenceEquals(object? obj1, object? obj2) => throw null!;

                    [InterceptsLocation("Program.cs", 22, 13)]
                    public static bool NotReferenceEquals(object? obj1, object? obj2) => throw null!;
                }

                public class Void { }
                public struct Boolean { }
                public class String { }
                public class Attribute { }
                public abstract class Enum { }
                public enum AttributeTargets { }
                public class AttributeUsageAttribute : Attribute
                {
                    public AttributeUsageAttribute(AttributeTargets targets) { }
                    public bool AllowMultiple { get; set; }
                    public bool Inherited { get; set; }
                }
                public class Exception { }
                public abstract class ValueType { }
                public struct Int32 { }
                public struct Byte { }
            }

            namespace System.Runtime.CompilerServices
            {
                public sealed class InterceptableAttribute : Attribute { }

                public sealed class InterceptsLocationAttribute : Attribute
                {
                    public InterceptsLocationAttribute(string filePath, int line, int column)
                    {
                    }
                }
            }
            """;
        var verifier = CompileAndVerify(CreateEmptyCompilation((source, "Program.cs"), parseOptions: RegularWithInterceptors, options: WithNullableEnable()), verify: Verification.Skipped);
        verifier.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Verify();

        var referenceEqualsCallIL = """
            {
              // Code size        7 (0x7)
              .maxstack  2
              IL_0000:  ldarg.0
              IL_0001:  ldarg.1
              IL_0002:  bne.un.s   IL_0006
              IL_0004:  ldnull
              IL_0005:  throw
              IL_0006:  ret
            }
            """;
        verifier.VerifyIL("D.M0", referenceEqualsCallIL);
        verifier.VerifyIL("D.M1", referenceEqualsCallIL);

        verifier.VerifyIL("D.M2", """
            {
              // Code size       12 (0xc)
              .maxstack  2
              IL_0000:  ldarg.0
              IL_0001:  ldarg.1
              IL_0002:  call       "bool object.NotReferenceEquals(object, object)"
              IL_0007:  brfalse.s  IL_000b
              IL_0009:  ldnull
              IL_000a:  throw
              IL_000b:  ret
            }
            """);
    }

    [Fact]
    public void ReferenceEquals_02()
    {
        // Intercept a call to object.ReferenceEquals
        var source = """
            using System.Runtime.CompilerServices;

            static class D
            {
                public static void M0(object? obj1, object? obj2)
                {
                    if (object.ReferenceEquals(obj1, obj2))
                       throw null!;
                }

                [InterceptsLocation("Program.cs", 7, 20)]
                public static bool Interceptor(object? obj1, object? obj2)
                {
                    return false;
                }
            }

            namespace System
            {
                public class Object
                {

                    public static bool ReferenceEquals(object? obj1, object? obj2) => throw null!;
                }

                public class Void { }
                public struct Boolean { }
                public class String { }
                public class Attribute { }
                public abstract class Enum { }
                public enum AttributeTargets { }
                public class AttributeUsageAttribute : Attribute
                {
                    public AttributeUsageAttribute(AttributeTargets targets) { }
                    public bool AllowMultiple { get; set; }
                    public bool Inherited { get; set; }
                }
                public class Exception { }
                public abstract class ValueType { }
                public struct Int32 { }
                public struct Byte { }
            }

            namespace System.Runtime.CompilerServices
            {
                public sealed class InterceptableAttribute : Attribute { }

                public sealed class InterceptsLocationAttribute : Attribute
                {
                    public InterceptsLocationAttribute(string filePath, int line, int column)
                    {
                    }
                }
            }
            """;
        var verifier = CompileAndVerify(CreateEmptyCompilation((source, "Program.cs"), parseOptions: RegularWithInterceptors, options: WithNullableEnable()), verify: Verification.Skipped);
        verifier.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Verify();

        verifier.VerifyIL("D.M0", """
            {
              // Code size       12 (0xc)
              .maxstack  2
              IL_0000:  ldarg.0
              IL_0001:  ldarg.1
              IL_0002:  call       "bool D.Interceptor(object, object)"
              IL_0007:  brfalse.s  IL_000b
              IL_0009:  ldnull
              IL_000a:  throw
              IL_000b:  ret
            }
            """);
    }

    [Fact]
    public void ParamsMismatch_01()
    {
        // Test when interceptable method has 'params' parameter.
        var source = """
            class C
            {

                public static void InterceptableMethod(params int[] value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod(1, 2, 3);
                    C.InterceptableMethod(4, 5, 6);
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(int[] value)
                {
                    foreach (var i in value)
                        Console.Write(i);
                }

                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor2(params int[] value)
                {
                    foreach (var i in value)
                        Console.Write(i);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "123456");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void ParamsMismatch_02()
    {
        // Test when interceptable method lacks 'params' parameter, and interceptor has one, and method is called as if it has one.
        var source = """
            class C
            {

                public static void InterceptableMethod(int[] value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod(1, 2, 3 ); // 1
                    C.InterceptableMethod(4, 5, 6); // 2
                }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(int[] value)
                {
                    foreach (var i in value)
                        Console.Write(i);
                }

                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor2(params int[] value)
                {
                    foreach (var i in value)
                        Console.Write(i);
                }
            }
            """;
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (11,11): error CS1501: No overload for method 'InterceptableMethod' takes 3 arguments
            //         C.InterceptableMethod(1, 2, 3 ); // 1
            Diagnostic(ErrorCode.ERR_BadArgCount, "InterceptableMethod").WithArguments("InterceptableMethod", "3").WithLocation(11, 11),
            // (12,11): error CS1501: No overload for method 'InterceptableMethod' takes 3 arguments
            //         C.InterceptableMethod(4, 5, 6); // 2
            Diagnostic(ErrorCode.ERR_BadArgCount, "InterceptableMethod").WithArguments("InterceptableMethod", "3").WithLocation(12, 11));
    }

    [Fact]
    public void ParamsMismatch_03()
    {
        // Test when interceptable method lacks 'params' parameter, and interceptor has one, and method is called in normal form.
        var source = """
            class C
            {

                public static void InterceptableMethod(int[] value) => throw null!;
            }

            static class Program
            {
                public static void Main()
                {
                    C.InterceptableMethod(new[] { 1, 2, 3 });
                    C.InterceptableMethod(new[] { 4, 5, 6 });
                }
            }
            """;

        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor1(int[] value)
                {
                    foreach (var i in value)
                        Console.Write(i);
                }

                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static void Interceptor2(params int[] value)
                {
                    foreach (var i in value)
                        Console.Write(i);
                }
            }
            """;
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "123456");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterpolatedStringHandler_01()
    {
        // Verify that interpolated string-related attributes on an intercepted call use the attributes from the interceptable method.
        var code = """
            using System;
            using System.Runtime.CompilerServices;

            var s = new S1();
            s.M($"");

            public struct S1
            {
                public S1() { }
                public int Field = 1;


                public void M([InterpolatedStringHandlerArgument("")] CustomHandler c)
                {
                    Console.Write(0);
                }
            }

            partial struct CustomHandler
            {
                public CustomHandler(int literalLength, int formattedCount, S1 s)
                {
                    Console.Write(1);
                }
            }
            """;
        var locations = GetInterceptableLocations(code);
        var interceptor = $$"""
            using System;
            using System.Runtime.CompilerServices;

            public static class S1Ext
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1(ref this S1 s1, CustomHandler c)
                {
                    Console.Write(2);
                }
            }
            """;
        var verifier = CompileAndVerify([
            code,
            interceptor,
            InterpolatedStringHandlerArgumentAttribute,
            GetInterpolatedStringCustomHandlerType("CustomHandler", "partial struct", useBoolReturns: false),
            s_attributesSource],
            parseOptions: RegularWithInterceptors,
            expectedOutput: "12");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterpolatedStringHandler_02()
    {
        // Verify that interpolated string-related attributes are ignored on an interceptor in an intercepted call.
        var code = """
using System;

var s = new S1();
s.M($"");

public struct S1
{
    public S1() { }
    public int Field = 1;


    public void M(CustomHandler c)
    {
        Console.Write(0);
    }
}

partial struct CustomHandler
{
    public CustomHandler(int literalLength, int formattedCount, S1 s)
    {
        throw null!; // we don't expect this to be called
    }
}
""";
        var locations = GetInterceptableLocations(code);
        var interceptors = $$"""
using System.Runtime.CompilerServices;
using System;

public static class S1Ext
{
    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void M1(ref this S1 s1, [InterpolatedStringHandlerArgument("s1")] CustomHandler c)
    {
        Console.Write(1);
    }
}
""";
        var verifier = CompileAndVerify([
            code,
            interceptors,
            InterpolatedStringHandlerArgumentAttribute,
            GetInterpolatedStringCustomHandlerType("CustomHandler", "partial struct", useBoolReturns: false),
            s_attributesSource],
            parseOptions: RegularWithInterceptors,
            expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterpolatedStringHandler_03()
    {
        // Verify that interpolated string attributes on an interceptor don't cause us to somehow pick a different argument.
        var code = """
using System;
using System.Runtime.CompilerServices;

var s1 = new S1(1);
var s2 = new S1(2);
S1.M(s1, s2, $"");

public struct S1
{
    public S1(int field) => Field = field;
    public int Field = 1;


    public static void M(S1 s1, S1 s2, [InterpolatedStringHandlerArgument("s1")] CustomHandler c)
    {
        Console.Write(0);
    }
}

partial struct CustomHandler
{
    public CustomHandler(int literalLength, int formattedCount, S1 s)
    {
        Console.Write(s.Field);
    }
}
""";
        var locations = GetInterceptableLocations(code);
        var interceptors = $$"""
using System.Runtime.CompilerServices;
using System;

public static class S1Ext
{
    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void M1(S1 s2, S1 s3, [InterpolatedStringHandlerArgument("s2")] CustomHandler c)
    {
        Console.Write(2);
    }
}
""";
        var verifier = CompileAndVerify([
                code,
                interceptors,
                InterpolatedStringHandlerArgumentAttribute,
                GetInterpolatedStringCustomHandlerType("CustomHandler", "partial struct", useBoolReturns: false),
                s_attributesSource
            ],
            parseOptions: RegularWithInterceptors,
            expectedOutput: "12");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void LineDirective_01()
    {
        // Verify that line directives are not considered when deciding if a particular call is being intercepted.
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {

                public static void InterceptableMethod() { Console.Write("interceptable"); }

                public static void Main()
                {
                    #line 42 "OtherFile.cs"
                    InterceptableMethod();
                }
            }

            class D
            {
                [InterceptsLocation("Program.cs", 12, 9)]
                public static void Interceptor1() { Console.Write("interceptor 1"); }
            }
            """;
        var verifier = CompileAndVerify(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "interceptor 1");
        verifier.VerifyDiagnostics(
            // OtherFile.cs(48,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 12, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 12, 9)").WithLocation(48, 6));
    }

    [Fact]
    public void LineDirective_02()
    {
        // Verify that line directives are not considered when deciding if a particular call is being intercepted.
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {

                public static void InterceptableMethod() { Console.Write("interceptable"); }

                public static void Main()
                {
                    #line 42 "OtherFile.cs"
                    InterceptableMethod();
                }
            }

            class D
            {
                [InterceptsLocation("OtherFile.cs", 42, 9)]
                public static void Interceptor1() { Console.Write("interceptor 1"); }
            }
            """;
        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // OtherFile.cs(48,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("OtherFile.cs", 42, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""OtherFile.cs"", 42, 9)").WithLocation(48, 6),
            // OtherFile.cs(48,25): error CS9139: Cannot intercept: compilation does not contain a file with path 'OtherFile.cs'.
            //     [InterceptsLocation("OtherFile.cs", 42, 9)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""OtherFile.cs""").WithArguments("OtherFile.cs").WithLocation(48, 25));
    }

    [Fact]
    public void ObsoleteInterceptor()
    {
        // Expect no Obsolete diagnostics to be reported
        var source = """
            C.M();

            class C
            {

                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [Obsolete]
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1() => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void CallerInfo()
    {
        // CallerLineNumber, etc. on the interceptor doesn't affect the default arguments passed to an intercepted call.
        var source = """
            C.M();

            class C
            {

                public static void M(int lineNumber = 1) => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1([CallerLineNumber] int lineNumber = 0) => Console.Write(lineNumber);
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void DefaultArguments_01()
    {
        // Default parameter values on the interceptor doesn't affect the default arguments passed to an intercepted call.
        var source = """
            C.M();

            class C
            {

                public static void M(int lineNumber = 1) => throw null!;
            }
            """;

        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1(int lineNumber = 0) => Console.Write(lineNumber);
            }
            """;

        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void DefaultArguments_02()
    {
        // Interceptor cannot add a default argument when original method lacks it.
        var source = """
            C.M(); // 1

            class C
            {
                public static void M(int lineNumber) => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void M1(int lineNumber = 0) => Console.Write(lineNumber);
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (1,3): error CS7036: There is no argument given that corresponds to the required parameter 'lineNumber' of 'C.M(int)'
            // C.M(); // 1
            Diagnostic(ErrorCode.ERR_NoCorrespondingArgument, "M").WithArguments("lineNumber", "C.M(int)").WithLocation(1, 3));
    }

    [Fact]
    public void InterceptorExtern()
    {
        var source = """
            C.M();

            class C
            {

                public static void M() => throw null!;
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static extern void Interceptor();
            }
            """;

        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularWithInterceptors, verify: Verification.Skipped);
        verifier.VerifyDiagnostics();

        verifier.VerifyIL("<top-level-statements-entry-point>", """
            {
              // Code size        6 (0x6)
              .maxstack  0
              IL_0000:  call       "void D.Interceptor()"
              IL_0005:  ret
            }
            """);
    }

    [Fact]
    public void InterceptorAbstract()
    {
        var source = """
            var d = new D();
            d.M();

            abstract partial class C
            {
                public void M() => throw null!;
            }

            partial class D { }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptor = $$"""
            using System.Runtime.CompilerServices;
            using System;

            abstract partial class C
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public abstract void Interceptor();
            }

            partial class D : C
            {
                public override void Interceptor() => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptorInterface()
    {
        var source = """
            using System;

            I i = new C();
            i.M();

            partial interface I
            {
                public void M();
            }

            class C : I
            {
                public void M() => throw null!;
                public void Interceptor() => Console.Write(1);
            }
            """;

        var location = GetInterceptableLocations(source)[0]!;
        var interceptor = $$"""
            using System.Runtime.CompilerServices;

            partial interface I
            {
                [InterceptsLocation({{GetAttributeArgs(location)}})]
                void Interceptor();
            }
            """;

        var verifier = CompileAndVerify([source, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void InterceptGetEnumerator()
    {
        var source = """
            using System.Collections;
            using System.Runtime.CompilerServices;

            var myEnumerable = new MyEnumerable();
            foreach (var item in myEnumerable)
            {
            }

            class MyEnumerable : IEnumerable
            {
                public IEnumerator GetEnumerator() => throw null!;
            }

            static class MyEnumerableExt
            {
                [InterceptsLocation("Program.cs", 5, 22)] // 1
                public static IEnumerator GetEnumerator1(this MyEnumerable en) => throw null!;
            }
            """;

        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(16,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 5, 22)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 5, 22)").WithLocation(16, 6),
            // Program.cs(16,6): error CS9151: Possible method name 'myEnumerable' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 5, 22)] // 1
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 5, 22)").WithArguments("myEnumerable").WithLocation(16, 6));
    }

    [Fact]
    public void InterceptDispose()
    {
        var source = """
            using System;
            using System.Runtime.CompilerServices;

            var myDisposable = new MyDisposable();
            using (myDisposable)
            {
            }

            class MyDisposable : IDisposable
            {
                public void Dispose() => throw null!;
            }

            static class MyDisposeExt
            {
                [InterceptsLocation("Program.cs", 5, 8)] // 1
                public static void Dispose1(this MyDisposable md) => throw null!;
            }
            """;

        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(16,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 5, 8)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 5, 8)").WithLocation(16, 6),
            // Program.cs(16,6): error CS9151: Possible method name 'myDisposable' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 5, 8)] // 1
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 5, 8)").WithArguments("myDisposable").WithLocation(16, 6)
            );
    }

    [Fact]
    public void InterceptDeconstruct()
    {
        var source = """
            using System;
            using System.Runtime.CompilerServices;

            var myDeconstructable = new MyDeconstructable();
            var (x, y) = myDeconstructable;

            class MyDeconstructable
            {
                public void Deconstruct(out int x, out int y) => throw null!;
            }

            static class MyDeconstructableExt
            {
                [InterceptsLocation("Program.cs", 5, 14)] // 1
                public static void Deconstruct1(this MyDeconstructable md, out int x, out int y) => throw null!;
            }
            """;

        var comp = CreateCompilation(new[] { (source, "Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("Program.cs", 5, 14)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""Program.cs"", 5, 14)").WithLocation(14, 6),
            // Program.cs(14,6): error CS9151: Possible method name 'myDeconstructable' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("Program.cs", 5, 14)] // 1
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""Program.cs"", 5, 14)").WithArguments("myDeconstructable").WithLocation(14, 6)
            );
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/76126")]
    public void DisplayPathMapping_01()
    {
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var resolver = new SourceFileResolver([], null, [new KeyValuePair<string, string>(pathPrefix, "/_/")]);
        var options = TestOptions.DebugExe.WithSourceReferenceResolver(resolver);

        var source = ("""
            C c = new C();
            c.M();
            """, path);
        var comp = CreateCompilation(source, options: options);
        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var location = model.GetInterceptableLocation(node)!;
        Assert.Equal("/_/Program.cs(2,3)", location.GetDisplayLocation());

        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation({{GetAttributeArgs(location)}})]
                public void Interceptor() => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify(
            [source, interceptors, s_attributesSource],
            parseOptions: RegularWithInterceptors,
            options: options,
            expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void PathMapping_01()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation("/_/Program.cs", 5, 3)]
                public void Interceptor() => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, "/_/"));

        var verifier = CompileAndVerify(
            new[] { (source, path), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)),
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\My\Machine\Specific\Path\Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("/_/Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""/_/Program.cs"", 5, 3)").WithLocation(11, 6));
    }

    [Fact]
    public void PathMapping_02()
    {
        // Attribute contains an unmapped path even though compilation uses a pathmap.
        // Because normalizing to the path of the containing file also effectively applies the pathmap, we accept the given path
        var pathPrefix = PlatformInformation.IsWindows ? @"C:\My\Machine\Specific\Path\" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var source = $$"""
            using System.Runtime.CompilerServices;
            using System;

            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation(@"{{path}}", 5, 3)]
                public void Interceptor() => Console.Write(1);
            }
            """;
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, "/_/"));

        var verifier = CompileAndVerify(
            new[] { (source, path), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)),
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\My\Machine\Specific\Path\Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"C:\My\Machine\Specific\Path\Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, $@"InterceptsLocation(@""{path}"", 5, 3)").WithLocation(11, 6));
    }

    [Fact]
    public void PathMapping_03()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation(@"\_\Program.cs", 5, 3)]
                public void Interceptor() => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, "/_/"));

        var comp = CreateCompilation(
            new[] { (source, path), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)));
        comp.VerifyEmitDiagnostics([
            // C:\My\Machine\Specific\Path\Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"\_\Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""\_\Program.cs"", 5, 3)").WithLocation(11, 6),
            ..(ReadOnlySpan<DiagnosticDescription>)[PlatformInformation.IsWindows
                // C:\My\Machine\Specific\Path\Program.cs(11,25): error CS9139: Cannot intercept: compilation does not contain a file with path 'C:\_\Program.cs'.
                //     [InterceptsLocation(@"\_\Program.cs", 5, 3)]
                ? Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"@""\_\Program.cs""").WithArguments(@"C:\_\Program.cs").WithLocation(11, 25)

                // /My/Machine/Specific/Path/Program.cs(11,25): error CS9139: Cannot intercept: compilation does not contain a file with path '/My/Machine/Specific/Path/\_\Program.cs'.
                //     [InterceptsLocation(@"\_\Program.cs", 5, 3)]
                : Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"@""\_\Program.cs""").WithArguments(@"/My/Machine/Specific/Path/\_\Program.cs").WithLocation(11, 25)]
            ]);
    }

    [Fact]
    public void PathMapping_04()
    {
        // Test when unmapped file paths are distinct, but mapped paths are equal.
        var source1 = """
            using System.Runtime.CompilerServices;
            using System;

            namespace NS1;

            class C
            {
                public static void M0()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;

                [InterceptsLocation(@"/_/Program.cs", 11, 9)]
                public void Interceptor() => Console.Write(1);
            }
            """;

        var source2 = """
            using System.Runtime.CompilerServices;
            using System;

            namespace NS2;

            class C
            {
                public static void M0()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        var pathPrefix1 = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path1\""" : "/My/Machine/Specific/Path1/";
        var pathPrefix2 = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path2\""" : "/My/Machine/Specific/Path2/";
        var path1 = pathPrefix1 + "Program.cs";
        var path2 = pathPrefix2 + "Program.cs";
        var pathMap = ImmutableArray.Create(
            new KeyValuePair<string, string>(pathPrefix1, "/_/"),
            new KeyValuePair<string, string>(pathPrefix2, "/_/")
            );

        var comp = CreateCompilation(
            new[] { (source1, path1), (source2, path2), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugDll.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)));
        comp.VerifyEmitDiagnostics(
            // C:\My\Machine\Specific\Path1\Program.cs(16,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"/_/Program.cs", 11, 9)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""/_/Program.cs"", 11, 9)").WithLocation(16, 6),
            // C:\My\Machine\Specific\Path1\Program.cs(16,25): error CS9152: Cannot intercept a call in file with path '/_/Program.cs' because multiple files in the compilation have this path.
            //     [InterceptsLocation(@"/_/Program.cs", 11, 9)]
            Diagnostic(ErrorCode.ERR_InterceptorNonUniquePath, @"@""/_/Program.cs""").WithArguments("/_/Program.cs").WithLocation(16, 25));
    }

    [Fact]
    public void PathMapping_05()
    {
        // Pathmap replacement contains backslashes, and attribute path contains backslashes.
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation(@"\_\Program.cs", 5, 3)]
                public void Interceptor() => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, @"\_\"));

        var verifier = CompileAndVerify(
            new[] { (source, path), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)),
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\My\Machine\Specific\Path\Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"\_\Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""\_\Program.cs"", 5, 3)").WithLocation(11, 6));
    }

    [Fact]
    public void PathMapping_06()
    {
        // Pathmap mixes slashes and backslashes, attribute path is normalized to slashes
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation(@"/_/Program.cs", 5, 3)]
                public void Interceptor() => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, @"\_/"));

        var comp = CreateCompilation(
            [(source, path), s_attributesSource],
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)));
        comp.VerifyEmitDiagnostics([
            // C:\My\Machine\Specific\Path\Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"/_/Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""/_/Program.cs"", 5, 3)").WithLocation(11, 6),
            ..(ReadOnlySpan<DiagnosticDescription>)[PlatformInformation.IsWindows
                // C:\My\Machine\Specific\Path\Program.cs(11,25): error CS9139: Cannot intercept: compilation does not contain a file with path 'C:\_\Program.cs'.
                //     [InterceptsLocation(@"/_/Program.cs", 5, 3)]
                ? Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"@""/_/Program.cs""").WithArguments(PlatformInformation.IsWindows ? @"C:\_\Program.cs" : "/_/Program.cs").WithLocation(11, 25)

                // /My/Machine/Specific/Path/Program.cs(11,25): error CS9139: Cannot intercept: compilation does not contain a file with path '/_/Program.cs'.
                //     [InterceptsLocation(@"/_/Program.cs", 5, 3)]
                : Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"@""/_/Program.cs""").WithArguments("/_/Program.cs").WithLocation(11, 25)]]);
    }

    [Fact]
    public void PathMapping_07()
    {
        // Pathmap replacement mixes slashes and backslashes, attribute path matches it
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;

                [InterceptsLocation(@"\_/Program.cs", 5, 3)]
                public void Interceptor() => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "Program.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, @"\_/"));

        var verifier = CompileAndVerify(
            new[] { (source, path), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)),
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\My\Machine\Specific\Path\Program.cs(11,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"\_/Program.cs", 5, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""\_/Program.cs"", 5, 3)").WithLocation(11, 6));
    }

    [Fact]
    public void PathNormalization_01()
    {
        // No pathmap is present and slashes in the attribute match the FilePath on the syntax tree.
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;

                [InterceptsLocation("src/Program.cs", 9, 11)]
                public void Interceptor() => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify(
            new[] { (source, "src/Program.cs"), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // src/Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("src/Program.cs", 9, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""src/Program.cs"", 9, 11)").WithLocation(14, 6));
    }

    [Fact]
    public void PathNormalization_02()
    {
        // No pathmap is present and backslashes in the attribute match the FilePath on the syntax tree.
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;

                [InterceptsLocation(@"src\Program.cs", 9, 11)]
                public void Interceptor() => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify(
            new[] { (source, @"src\Program.cs"), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // src\Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"src\Program.cs", 9, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""src\Program.cs"", 9, 11)").WithLocation(14, 6));
    }

    [Fact]
    public void PathNormalization_03()
    {
        // Relative paths do not have slashes normalized when pathmap is not present
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;

                [InterceptsLocation(@"src/Program.cs", 9, 11)]
                public void Interceptor() => Console.Write(1);
            }
            """;

        var comp = CreateCompilation(new[] { (source, @"src\Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // src\Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation(@"src/Program.cs", 9, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""src/Program.cs"", 9, 11)").WithLocation(14, 6),
            // src\Program.cs(14,25): error CS9140: Cannot intercept: compilation does not contain a file with path 'src/Program.cs'. Did you mean to use path 'src\Program.cs'?
            //     [InterceptsLocation(@"src/Program.cs", 9, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilationWithCandidate, @"@""src/Program.cs""").WithArguments("src/Program.cs", @"src\Program.cs").WithLocation(14, 25));
    }

    [Fact]
    public void PathNormalization_04()
    {
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;

                [InterceptsLocation("C:/src/Program.cs", 9, 11)]
                public void Interceptor() => Console.Write(1);
            }
            """;

        if (PlatformInformation.IsWindows)
        {
            var verifier = CompileAndVerify(new[] { (source, @"C:\src\Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "1");
            verifier.VerifyDiagnostics(
                // C:\src\Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
                //     [InterceptsLocation("C:/src/Program.cs", 9, 11)]
                Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""C:/src/Program.cs"", 9, 11)").WithLocation(14, 6));
        }
        else
        {
            var comp = CreateCompilation(new[] { (source, @"/src/Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
            comp.VerifyEmitDiagnostics(
                // C:\src\Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
                //     [InterceptsLocation("C:/src/Program.cs", 9, 11)]
                Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""C:/src/Program.cs"", 9, 11)").WithLocation(14, 6),
                // /src/Program.cs(14,25): error CS9139: Cannot intercept: compilation does not contain a file with path '/src/C:/src/Program.cs'.
                //     [InterceptsLocation("C:/src/Program.cs", 9, 11)]
                Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""C:/src/Program.cs""").WithArguments("/src/C:/src/Program.cs").WithLocation(14, 25));
        }
    }

    [Fact]
    public void PathNormalization_05()
    {
        // paths in attribute as well as syntax tree have mixed slashes
        var source = """
            using System.Runtime.CompilerServices;
            using System;

            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;

                [InterceptsLocation(@"C:\src/Program.cs", 9, 11)]
                public void Interceptor() => Console.Write(1);
            }
            """;

        if (PlatformInformation.IsWindows)
        {
            var verifier = CompileAndVerify(new[] { (source, @"C:/src\Program.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "1");
            verifier.VerifyDiagnostics(
                // C:/src\Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
                //     [InterceptsLocation(@"C:\src/Program.cs", 9, 11)]
                Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""C:\src/Program.cs"", 9, 11)").WithLocation(14, 6));
        }
        else
        {
            var comp = CreateCompilation(new[] { (source, @"/src/Program.`cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
            comp.VerifyEmitDiagnostics(
                // C:/src\Program.cs(14,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
                //     [InterceptsLocation(@"C:\src/Program.cs", 9, 11)]
                Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(@""C:\src/Program.cs"", 9, 11)").WithLocation(14, 6),
                // /src/Program.cs(14,25): error CS9139: Cannot intercept: compilation does not contain a file with path '/src/C:\src/Program.cs'.
                //     [InterceptsLocation(@"C:\src/Program.cs", 9, 11)]
                Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"@""C:\src/Program.cs""").WithArguments(@"/src/C:\src/Program.cs").WithLocation(14, 25));
        }
    }

    [Fact]
    public void RelativePaths_01()
    {
        var source = """
            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        var source2 = """
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation("../src/Program.cs", 6, 11)]
                internal static void Interceptor(this C c) => Console.Write(1);
            }
            """;

        var verifier = CompileAndVerify(new[] { (source, PlatformInformation.IsWindows ? @"C:\src\Program.cs" : "/src/Program.cs"), (source2, PlatformInformation.IsWindows ? @"C:\obj\Generated.cs" : "/obj/Generated.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\obj\Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/Program.cs", 6, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/Program.cs"", 6, 11)").WithLocation(6, 6));
    }

    [Fact]
    public void RelativePaths_02()
    {
        var source = """
            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        // interceptor containing file does not have absolute path
        // Therefore we don't resolve the relative path
        var source2 = """
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation("../src/Program.cs", 6, 11)]
                internal static void Interceptor(this C c) => Console.Write(1);
            }
            """;

        var comp = CreateCompilation(new[] { (source, PlatformInformation.IsWindows ? @"C:\src\Program.cs" : "/src/Program.cs"), (source2, PlatformInformation.IsWindows ? @"Generator\Generated.cs" : "Generator/Generated.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Generator\Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/Program.cs", 6, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/Program.cs"", 6, 11)").WithLocation(6, 6),
            // Generator\Generated.cs(6,25): error CS9139: Cannot intercept: compilation does not contain a file with path '../src/Program.cs'.
            //     [InterceptsLocation("../src/Program.cs", 6, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""../src/Program.cs""").WithArguments("../src/Program.cs").WithLocation(6, 25));
    }

    [Fact]
    public void RelativePaths_03()
    {
        // intercepted file does not have absolute path
        var source = """
            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        var source2 = """
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation("../src/Program.cs", 6, 11)]
                internal static void Interceptor(this C c) => Console.Write(1);
            }
            """;

        var comp = CreateCompilation(new[] { (source, PlatformInformation.IsWindows ? @"src\Program.cs" : "src/Program.cs"), (source2, PlatformInformation.IsWindows ? @"C:\obj\Generated.cs" : "/obj/Generated.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // C:\obj\Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/Program.cs", 6, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/Program.cs"", 6, 11)").WithLocation(6, 6),
            // C:\obj\Generated.cs(6,25): error CS9139: Cannot intercept: compilation does not contain a file with path 'C:\src\Program.cs'.
            //     [InterceptsLocation("../src/Program.cs", 6, 11)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""../src/Program.cs""").WithArguments(PlatformInformation.IsWindows ? @"C:\src\Program.cs" : "/src/Program.cs").WithLocation(6, 25)
            );
    }

    [Fact]
    public void RelativePaths_04()
    {
        var source = """
            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        // The relative path resolution of `C:\..` is just `C:\` (and `/..` resolves to `/`).
        var source2 = """
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation("../../src/Program.cs", 6, 11)]
                internal static void Interceptor(this C c) => Console.Write(1);
            }
            """;

        var comp = CreateCompilation(new[] { (source, PlatformInformation.IsWindows ? @"C:\src\Program.cs" : "/src/Program.cs"), (source2, PlatformInformation.IsWindows ? @"C:\obj\Generated.cs" : "/obj/Generated.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // C:\obj\Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../../src/Program.cs", 6, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../../src/Program.cs"", 6, 11)").WithLocation(6, 6));
    }

    [Fact]
    public void RelativePaths_05()
    {
        var source = """
            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        var source2 = """
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation("../src/./Program.cs", 6, 11)]
                internal static void Interceptor(this C c) => Console.Write(1);
            }
            """;

        var comp = CreateCompilation(new[] { (source, PlatformInformation.IsWindows ? @"C:\src\Program.cs" : "/src/Program.cs"), (source2, PlatformInformation.IsWindows ? @"C:\obj\Generated.cs" : "/obj/Generated.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // C:\obj\Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/./Program.cs", 6, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/./Program.cs"", 6, 11)").WithLocation(6, 6));
    }

    [Fact]
    public void RelativePaths_06()
    {
        var source = """
            class C
            {
                public static void Main()
                {
                    C c = new C();
                    c.M();
                }

                public void M() => throw null!;
            }
            """;

        var source2 = """
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation("../src/Program.cs/.", 6, 11)]
                internal static void Interceptor(this C c) => Console.Write(1);
            }
            """;

        var comp = CreateCompilation(new[] { (source, PlatformInformation.IsWindows ? @"C:\src\Program.cs" : "/src/Program.cs"), (source2, PlatformInformation.IsWindows ? @"C:\obj\Generated.cs" : "/obj/Generated.cs"), s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // C:\obj\Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/Program.cs/.", 6, 11)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/Program.cs/."", 6, 11)").WithLocation(6, 6));
    }

    [Fact]
    public void RelativePaths_07()
    {
        var source = """
            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;
            }
            """;

        var source2 = """
            using System.Runtime.CompilerServices;
            using System;

            static class Interceptors
            {
                [InterceptsLocation("../src/Program.cs", 2, 3)]
                public static void Interceptor(this C c) => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """C:\My\Machine\Specific\Path\""" : "/My/Machine/Specific/Path/";
        var path = pathPrefix + "src/Program.cs";
        var path2 = pathPrefix + "obj/Generated.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, "/_/"));

        var verifier = CompileAndVerify(
            new[] { (source, path), (source2, path2), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)),
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\My\Machine\Specific\Path\obj/Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/Program.cs", 2, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/Program.cs"", 2, 3)").WithLocation(6, 6));
    }

    [Fact]
    public void RelativePaths_08()
    {
        // SyntaxTree file paths are not absolute. Relative path resolution is not performed.
        var source = """
            C c = new C();
            c.M();

            class C
            {
                public void M() => throw null!;
            }
            """;

        var source2 = """
            using System.Runtime.CompilerServices;
            using System;

            static class Interceptors
            {
                [InterceptsLocation("../src/Program.cs", 2, 3)]
                public static void Interceptor(this C c) => Console.Write(1);
            }
            """;
        var pathPrefix = PlatformInformation.IsWindows ? """My\Machine\Specific\Path\""" : "My/Machine/Specific/Path/";
        var path = pathPrefix + "src/Program.cs";
        var path2 = pathPrefix + "obj/Generated.cs";
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(pathPrefix, "/_/"));

        var comp = CreateCompilation(
            new[] { (source, path), (source2, path2), s_attributesSource },
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)));
        comp.VerifyEmitDiagnostics(
            // My\Machine\Specific\Path\obj/Generated.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("../src/Program.cs", 2, 3)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""../src/Program.cs"", 2, 3)").WithLocation(6, 6),
            // My\Machine\Specific\Path\obj/Generated.cs(6,25): error CS9139: Cannot intercept: compilation does not contain a file with path '../src/Program.cs'.
            //     [InterceptsLocation("../src/Program.cs", 2, 3)]
            Diagnostic(ErrorCode.ERR_InterceptorPathNotInCompilation, @"""../src/Program.cs""").WithArguments("../src/Program.cs").WithLocation(6, 25));
    }

    [Fact]
    public void OldVersusNewResolutionStrategy()
    {
        // relative path resolution will match a file (and the node referenced is not interceptable)
        // exact mapped resolution will match a *different* file (and the node referenced is interceptable)
        var source1 = ("""
            class C1
            {
                void M1()
                {
                    var _ =
                        C.Interceptable;
                }
            }
            """, PlatformInformation.IsWindows ? @"C:\src1\file1.cs" : "/src1/file1.cs");

        var directory2 = PlatformInformation.IsWindows ? @"C:\src2\" : "/src2/";
        var path2 = PlatformInformation.IsWindows ? @"C:\src2\file1.cs" : "/src2/file1.cs";
        var source2 = ("""
            class C2
            {
                static void Main()
                {
                    // var _ =
                        C.Interceptable();
                }
            }

            class C
            {
                public static void Interceptable() => throw null!;
            }
            """, path2);

        var source3 = ("""
            using System.Runtime.CompilerServices;
            using System;

            class Interceptors
            {
                [InterceptsLocation("./file1.cs", 6, 15)] // 1
                public static void Interceptor() => Console.Write(1);
            }
            """, PlatformInformation.IsWindows ? @"C:\src1\interceptors.cs" : "/src1/interceptors.cs");

        // Demonstrate that "relative path" resolution happens first by triggering the not interceptable error.
        var pathMap = ImmutableArray.Create(new KeyValuePair<string, string>(directory2, "./"));
        var comp = CreateCompilation([source1, source2, source3, s_attributesSource],
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)));
        comp.VerifyEmitDiagnostics(
            // C:\src1\interceptors.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("./file1.cs", 6, 15)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""./file1.cs"", 6, 15)").WithLocation(6, 6),
            // C:\src1\interceptors.cs(6,6): error CS9151: Possible method name 'Interceptable' cannot be intercepted because it is not being invoked.
            //     [InterceptsLocation("./file1.cs", 6, 15)] // 1
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, @"InterceptsLocation(""./file1.cs"", 6, 15)").WithArguments("Interceptable").WithLocation(6, 6));

        // excluding 'source1' from the compilation, we fall back to exact match of mapped path, and interception is successful.
        var verifier = CompileAndVerify([source2, source3, s_attributesSource],
            parseOptions: RegularWithInterceptors,
            options: TestOptions.DebugExe.WithSourceReferenceResolver(
                new SourceFileResolver(ImmutableArray<string>.Empty, null, pathMap)),
            expectedOutput: "1");
        verifier.VerifyDiagnostics(
            // C:\src1\interceptors.cs(6,6): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //     [InterceptsLocation("./file1.cs", 6, 15)] // 1
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"InterceptsLocation(""./file1.cs"", 6, 15)").WithLocation(6, 6));
    }

    [Fact]
    public void InterceptorUnmanagedCallersOnly()
    {
        var source = """
            C.Interceptable();

            class C
            {
                public static void Interceptable() { }
            }
            """;
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                [UnmanagedCallersOnly]
                public static void Interceptor() { }
            }
            """;

        var comp = CreateCompilation([source, interceptors, s_attributesSource, UnmanagedCallersOnlyAttributeDefinition], parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (6,6): error CS9161: An interceptor cannot be marked with 'UnmanagedCallersOnlyAttribute'.
            //     [InterceptsLocation(1, "5P8UiY8bLUVHhLVapnhynAIAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorCannotUseUnmanagedCallersOnly, "InterceptsLocation").WithLocation(6, 6));
    }

    [Fact]
    public void InterceptorUnmanagedCallersOnly_Checksum()
    {
        var source = CSharpTestSource.Parse("""
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;
            using System;

            C.Interceptable();

            class C
            {
                public static void Interceptable() { }
            }
            """, "Program.cs", RegularWithInterceptors);
        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System.Runtime.CompilerServices;
            using System.Runtime.InteropServices;

            static class D
            {
                [InterceptsLocation({{locationSpecifier.Version}}, "{{locationSpecifier.Data}}")]
                [UnmanagedCallersOnly]
                public static void Interceptor() { }
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, s_attributesTree, CSharpTestSource.Parse(UnmanagedCallersOnlyAttributeDefinition, "UnmanagedCallersOnlyAttribute.cs", RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9161: An interceptor cannot be marked with 'UnmanagedCallersOnlyAttribute'.
            //     [InterceptsLocation(1, "SnNcyOJQR8oIDrJpnwBmCWIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorCannotUseUnmanagedCallersOnly, "InterceptsLocation").WithLocation(6, 6));
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70841")]
    public void InterceptorEnumBaseMethod()
    {
        var program = ("""
            using System;

            var value = MyEnum.Second;
            Console.WriteLine(value.ToString());

            public enum MyEnum
            {
                First,
                Second,
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(program);
        var interceptor = ($$"""
            using System.Runtime.CompilerServices;

            namespace MyInterceptors
            {
                public static class Interceptors
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                    public static string OtherToString(this System.Enum value)
                        => "Wrong Value" + value;
                }
            }
            """, "Interceptor.cs");

        var verifier = CompileAndVerify(new[] { program, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Second");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("<top-level-statements-entry-point>", """
            {
              // Code size       21 (0x15)
              .maxstack  1
              .locals init (MyEnum V_0) //value
              IL_0000:  ldc.i4.1
              IL_0001:  stloc.0
              IL_0002:  ldloca.s   V_0
              IL_0004:  constrained. "MyEnum"
              IL_000a:  callvirt   "string object.ToString()"
              IL_000f:  call       "void System.Console.WriteLine(string)"
              IL_0014:  ret
            }
            """);

        verifier = CompileAndVerify(new[] { program, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Wrong ValueSecond");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("<top-level-statements-entry-point>", """
            {
              // Code size       17 (0x11)
              .maxstack  1
              IL_0000:  ldc.i4.1
              IL_0001:  box        "MyEnum"
              IL_0006:  call       "string MyInterceptors.Interceptors.OtherToString(System.Enum)"
              IL_000b:  call       "void System.Console.WriteLine(string)"
              IL_0010:  ret
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70841")]
    public void InterceptorStructBaseMethod()
    {
        var program = ("""
            using System;

            MyStruct value = default;
            Console.WriteLine(value.Equals((object)1));

            public struct MyStruct { }
            """, "Program.cs");
        var locations = GetInterceptableLocations(program);
        var interceptor = ($$"""
            using System.Runtime.CompilerServices;

            namespace MyInterceptors
            {
                public static class Interceptors
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                    public static bool Equals(this System.ValueType value, object other) => true;
                }
            }
            """, "Interceptor.cs");

        var verifier = CompileAndVerify(new[] { program, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "False");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("<top-level-statements-entry-point>", """
            {
              // Code size       33 (0x21)
              .maxstack  2
              .locals init (MyStruct V_0) //value
              IL_0000:  ldloca.s   V_0
              IL_0002:  initobj    "MyStruct"
              IL_0008:  ldloca.s   V_0
              IL_000a:  ldc.i4.1
              IL_000b:  box        "int"
              IL_0010:  constrained. "MyStruct"
              IL_0016:  callvirt   "bool object.Equals(object)"
              IL_001b:  call       "void System.Console.WriteLine(bool)"
              IL_0020:  ret
            }
            """);

        verifier = CompileAndVerify(new[] { program, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "True");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("<top-level-statements-entry-point>", """
            {
              // Code size       31 (0x1f)
              .maxstack  2
              .locals init (MyStruct V_0)
              IL_0000:  ldloca.s   V_0
              IL_0002:  initobj    "MyStruct"
              IL_0008:  ldloc.0
              IL_0009:  box        "MyStruct"
              IL_000e:  ldc.i4.1
              IL_000f:  box        "int"
              IL_0014:  call       "bool MyInterceptors.Interceptors.Equals(System.ValueType, object)"
              IL_0019:  call       "void System.Console.WriteLine(bool)"
              IL_001e:  ret
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70841")]
    public void InterceptorTypeParameterObjectMethod()
    {
        var program = ("""
            using System;

            M("a");
            void M<T>(T value)
            {
                Console.WriteLine(value.Equals((object)1));
            }

            public struct MyStruct { }
            """, "Program.cs");

        var locations = GetInterceptableLocations(program);
        var location = locations[2]!;

        var interceptor = ($$"""
            using System.Runtime.CompilerServices;

            namespace MyInterceptors
            {
                public static class Interceptors
                {
                    [InterceptsLocation({{GetAttributeArgs(location)}})]
                    public static new bool Equals(this object value, object other) => true;
                }
            }
            """, "Interceptor.cs");

        var verifier = CompileAndVerify([program, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "False");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("Program.<<Main>$>g__M|0_0<T>(T)", """
            {
              // Code size       25 (0x19)
              .maxstack  2
              IL_0000:  ldarga.s   V_0
              IL_0002:  ldc.i4.1
              IL_0003:  box        "int"
              IL_0008:  constrained. "T"
              IL_000e:  callvirt   "bool object.Equals(object)"
              IL_0013:  call       "void System.Console.WriteLine(bool)"
              IL_0018:  ret
            }
            """);

        verifier = CompileAndVerify([program, interceptor, s_attributesSource], parseOptions: RegularWithInterceptors, expectedOutput: "True");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("Program.<<Main>$>g__M|0_0<T>(T)", """
            {
              // Code size       23 (0x17)
              .maxstack  2
              IL_0000:  ldarg.0
              IL_0001:  box        "T"
              IL_0006:  ldc.i4.1
              IL_0007:  box        "int"
              IL_000c:  call       "bool MyInterceptors.Interceptors.Equals(object, object)"
              IL_0011:  call       "void System.Console.WriteLine(bool)"
              IL_0016:  ret
            }
            """);
    }

    [Theory]
    [WorkItem("https://github.com/dotnet/roslyn/issues/70841")]
    [InlineData("where T : struct, I")]
    [InlineData("where T : I")]
    public void InterceptorStructConstrainedInterfaceMethod(string constraints)
    {
        var program = ($$"""
            using System;

            C.M(default(MyStruct));

            class C
            {
                public static void M<T>(T t) {{constraints}}
                {
                    t.IM();
                }
            }

            public struct MyStruct : I
            {
                public void IM()
                {
                    Console.Write("Original");
                }
            }

            public interface I
            {
                void IM();
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(program);
        var interceptor = ($$"""
            using System.Runtime.CompilerServices;
            using System;

            namespace MyInterceptors
            {
                public static class Interceptors
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                    public static void IM(this I @this) { Console.Write("Interceptor"); }
                }
            }
            """, "Interceptor.cs");

        var verifier = CompileAndVerify(new[] { program, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Original");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("C.M<T>(T)", """
            {
              // Code size       14 (0xe)
              .maxstack  1
              IL_0000:  ldarga.s   V_0
              IL_0002:  constrained. "T"
              IL_0008:  callvirt   "void I.IM()"
              IL_000d:  ret
            }
            """);

        verifier = CompileAndVerify(new[] { program, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Interceptor");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("C.M<T>(T)", """
            {
              // Code size       12 (0xc)
              .maxstack  1
              IL_0000:  ldarg.0
              IL_0001:  box        "T"
              IL_0006:  call       "void MyInterceptors.Interceptors.IM(I)"
              IL_000b:  ret
            }
            """);
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70311")]
    public void InterceptorGeneric_01()
    {
        var source = ("""
            #nullable enable
            using System;

            class C
            {
                public string Method1<T>(T arg) => "Original";
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C();
                    string? x = null;

                    Console.Write(c.Method1(x));
                }
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            #nullable enable
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static string Generic<T>(this C s, T arg) => "Interceptor";
            }
            """, "Interceptor.cs");

        var verifier = CompileAndVerify(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Original");
        verifier.VerifyDiagnostics();

        verifier = CompileAndVerify(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Interceptor");
        verifier.VerifyDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70311")]
    public void InterceptorGeneric_02()
    {
        var source = ("""
            #nullable enable
            using System;

            class C<T>
            {
                public string Method1<U>(U arg) => "Original";
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C<int>();
                    string? x = null;

                    Console.Write(c.Method1(x));
                }
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            #nullable enable
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
                public static string Generic<T, U>(this C<T> s, U arg) => "Interceptor";
            }
            """, "Interceptor.cs");

        var verifier = CompileAndVerify(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Original");
        verifier.VerifyDiagnostics();

        verifier = CompileAndVerify(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors, expectedOutput: "Interceptor");
        verifier.VerifyDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70311")]
    public void InterceptorGeneric_03()
    {
        var source = ("""
            #nullable enable

            class C<T>
            {
                public string? Method1<U>(U arg) => null;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C<int>();
                    string? x = null;

                    c.Method1(x);
                }
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            #nullable enable
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static string? Generic<T, U>(this T s, U arg) => arg?.ToString();
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics();

        comp = CreateCompilation(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(6,6): error CS9144: Cannot intercept method 'C<int>.Method1<string>(string)' with interceptor 'D.Generic<int, string>(int, string)' because the signatures do not match.
            //     [InterceptsLocation(1, "F9njcAIQ5lvPC9SOXWAkgtwAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "InterceptsLocation").WithArguments("C<int>.Method1<string>(string)", "D.Generic<int, string>(int, string)").WithLocation(6, 6)
        );
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70311")]
    public void InterceptorGeneric_04()
    {
        // interceptor type parameter substitution meets constraints
        var source = ("""
            #nullable enable

            class C<T>
            {
                public string? Method1<U>(U arg) => null;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C<int>();
                    string? x = null;

                    c.Method1(x);
                }
            }
            """, "Program.cs");

        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            #nullable enable
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static string? Generic<T, U>(this C<T> s, U arg) where T : struct => arg?.ToString();
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics();

        comp = CreateCompilation(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics();
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/70311")]
    public void InterceptorGeneric_05()
    {
        // interceptor type parameter substitution violates constraints
        var source = ("""
            #nullable enable

            class C<T>
            {
                public string? Method1<U>(U arg) => null;
            }

            static class Program
            {
                public static void Main()
                {
                    var c = new C<int>();
                    string? x = null;

                    c.Method1(x);
                }
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptor = ($$"""
            #nullable enable
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static string? Generic<T, U>(this C<T> s, U arg) where T : class => arg?.ToString();
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics();

        comp = CreateCompilation(new[] { source, interceptor, s_attributesSource }, parseOptions: RegularWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(6,6): error CS0452: The type 'int' must be a reference type in order to use it as parameter 'T' in the generic type or method 'D.Generic<T, U>(C<T>, U)'
            //     [InterceptsLocation(1, "F9njcAIQ5lvPC9SOXWAkgtwAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_RefConstraintNotSatisfied, "InterceptsLocation").WithArguments("D.Generic<T, U>(C<T>, U)", "T", "int").WithLocation(6, 6));
    }

    [Theory]
    [CombinatorialData]
    public void GetInterceptorMethod_01(bool checkBeforeDiagnostics)
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor() => Console.Write(1);
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: RegularWithInterceptors);

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        if (checkBeforeDiagnostics)
        {
            check();
        }

        comp.VerifyEmitDiagnostics();

        if (!checkBeforeDiagnostics)
        {
            check();
        }

        void check()
        {
            var interceptor = model.GetInterceptorMethod(call);
            Assert.Equal("void D.Interceptor()", interceptor.ToTestDisplayString());
        }
    }

    [Theory]
    [CombinatorialData]
    public void GetInterceptorMethod_02(bool checkBeforeDiagnostics)
    {
        var source = ("""
            C.M(42);

            class C
            {
                public static void M<T>(T t) => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class D
            {
                [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                public static void Interceptor<T>(T t) => Console.Write(t);
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: RegularWithInterceptors);

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        if (checkBeforeDiagnostics)
        {
            check();
        }

        comp.VerifyEmitDiagnostics();

        if (!checkBeforeDiagnostics)
        {
            check();
        }

        void check()
        {
            var interceptor = model.GetInterceptorMethod(call);
            Assert.Equal("void D.Interceptor<T>(T t)", interceptor.ToTestDisplayString());
            Assert.True(interceptor!.IsDefinition);
        }
    }

    [Fact]
    public void GetInterceptorMethod_03()
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void Interceptor() => Console.Write(1);
                }
            }

            class E : Attribute
            {
                [E]
                public void M()
                {
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.D.Interceptor()", interceptor.ToTestDisplayString());
        Assert.True(interceptor.GetSymbol()!.HasComplete(CompletionPart.Attributes));

        // Do not bind attributes on methods in irrelevant namespaces when discovering interceptors
        var EM = comp.GetMember<MethodSymbol>("E.M");
        Assert.False(EM.HasComplete(CompletionPart.Attributes));

        comp.VerifyEmitDiagnostics();

        Assert.True(EM.HasComplete(CompletionPart.Attributes));
    }

    [Fact]
    public void GetInterceptorMethod_04()
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");

        var location = GetInterceptableLocations(source)[0]!;

        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace NotInterceptors
            {
                static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(location)}})]
                    public static void Interceptor() => Console.Write(1);
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        // Interceptor declaration is erroneous (not within expected namespace), we don't care about failing to discover it.
        var interceptor = model.GetInterceptorMethod(call);
        Assert.Null(interceptor);

        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(8,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NotInterceptors</InterceptorsNamespaces>' to your project.
            //         [InterceptsLocation(1, "NnwjYrJAcjZ/s0+OSiPXqwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NotInterceptors</InterceptorsNamespaces>").WithLocation(8, 10));

        interceptor = model.GetInterceptorMethod(call);
        Assert.Null(interceptor);
    }

    [Fact]
    public void GetInterceptorMethod_05()
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void Interceptor() => Console.Write(1);
                }

                class E : Attribute
                {
                    [E]
                    public void M()
                    {
                    }
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.D.Interceptor()", interceptor.ToTestDisplayString());
        Assert.True(interceptor.GetSymbol()!.HasComplete(CompletionPart.Attributes));

        // Possibly irrelevant attributes within interceptors namespaces are still bound when discovering interceptors.
        // https://github.com/dotnet/roslyn/issues/72410: perhaps QuickAttributes should be used in order to bail out in some cases.
        var EM = comp.GetMember<MethodSymbol>("Interceptors.E.M");
        Assert.True(EM.HasComplete(CompletionPart.Attributes));

        comp.VerifyEmitDiagnostics();

        Assert.True(EM.HasComplete(CompletionPart.Attributes));
    }

    [Fact]
    public void GetInterceptorMethod_06()
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var location = GetInterceptableLocations(source)[0]!;
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(location)}})]
                    public static void Interceptor1(int i) => Console.Write(i);

                    [InterceptsLocation({{GetAttributeArgs(location)}})]
                    public static void Interceptor2() => Console.Write(2);
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Null(interceptor);

        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(8,10): error CS9153: The indicated call is intercepted multiple times.
            //         [InterceptsLocation(1, "NnwjYrJAcjZ/s0+OSiPXqwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(8, 10),
            // Interceptor.cs(11,10): error CS9153: The indicated call is intercepted multiple times.
            //         [InterceptsLocation(1, "NnwjYrJAcjZ/s0+OSiPXqwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_DuplicateInterceptor, "InterceptsLocation").WithLocation(11, 10));

        interceptor = model.GetInterceptorMethod(call);
        Assert.Null(interceptor);
    }

    [Fact]
    public void GetInterceptorMethod_07()
    {
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void Interceptor1() => Console.Write(1);
                }
            }

            namespace NotInterceptors
            {
                static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void Interceptor2() => Console.Write(2);
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.D.Interceptor1()", interceptor.ToTestDisplayString());

        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(17,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);NotInterceptors</InterceptorsNamespaces>' to your project.
            //         [InterceptsLocation(1, "NnwjYrJAcjZ/s0+OSiPXqwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);NotInterceptors</InterceptorsNamespaces>").WithLocation(17, 10));

        interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.D.Interceptor1()", interceptor.ToTestDisplayString());
    }

    [Fact]
    public void GetInterceptorMethod_08()
    {
        // Demonstrate that nested types are searched for InterceptsLocationAttributes
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                static class Outer
                {
                    public static class D
                    {
                        [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                        public static void Interceptor1() => Console.Write(1);
                    }
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.Outer.D.Interceptor1()", interceptor.ToTestDisplayString());

        comp.VerifyEmitDiagnostics();

        interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.Outer.D.Interceptor1()", interceptor.ToTestDisplayString());
    }

    [Theory]
    [CombinatorialData]
    public void GetInterceptorMethod_09(bool featureExists)
    {
        // InterceptorsNamespaces is empty or does not exist
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");

        var location = GetInterceptableLocations(source)[0]!;
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                static class Outer
                {
                    public static class D
                    {
                        [InterceptsLocation({{GetAttributeArgs(location)}})]
                        public static void Interceptor1() => Console.Write(1);
                    }
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: featureExists ? TestOptions.Regular.WithFeature("InterceptorsNamespaces", "") : TestOptions.Regular);

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        Assert.Null(model.GetInterceptorMethod(call));
        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(10,14): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);Interceptors</InterceptorsNamespaces>' to your project.
            //             [InterceptsLocation(1, "NnwjYrJAcjZ/s0+OSiPXqwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);Interceptors</InterceptorsNamespaces>").WithLocation(10, 14));
        Assert.Null(model.GetInterceptorMethod(call));
    }

    [Fact]
    public void GetInterceptorMethod_10()
    {
        // InterceptorsNamespaces has duplicates
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                static class Outer
                {
                    public static class D
                    {
                        [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                        public static void Interceptor1() => Console.Write(1);
                    }
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors;Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.Outer.D.Interceptor1()", interceptor.ToTestDisplayString());

        comp.VerifyEmitDiagnostics();

        interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.Outer.D.Interceptor1()", interceptor.ToTestDisplayString());
    }

    [Fact]
    public void GetInterceptorMethod_11()
    {
        // Compilation does not contain any interceptors
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");

        var comp = CreateCompilation(new[] { source, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Null(interceptor);

        comp.VerifyEmitDiagnostics();

        interceptor = model.GetInterceptorMethod(call);
        Assert.Null(interceptor);
    }

    [Fact]
    public void GetInterceptorMethod_12()
    {
        // Compilation contains no files
        var comp = CreateCompilation([], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors"));

        // We can't use GetInterceptorMethod without a SemanticModel and we can't get a SemanticModel when the compilation contains no trees.
        // But, we can exercise some internal API for theoretical edge cases to see if it is robust (does not throw, updates expected flags).
        ((SourceModuleSymbol)comp.SourceModule).DiscoverInterceptorsIfNeeded();
        Assert.True(comp.InterceptorsDiscoveryComplete);
    }

    [Theory]
    [InlineData("Interceptors")]
    [InlineData("Interceptors.Nested")]
    public void GetInterceptorMethod_13(string @namespace)
    {
        // Demonstrate that nested namespaces are searched for InterceptsLocationAttributes
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");
        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                namespace Nested
                {
                    public static class D
                    {
                        [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                        public static void Interceptor1() => Console.Write(1);
                    }
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation(new[] { source, interceptorSource, s_attributesSource }, parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", @namespace));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        var interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.Nested.D.Interceptor1()", interceptor.ToTestDisplayString());

        comp.VerifyEmitDiagnostics();

        interceptor = model.GetInterceptorMethod(call);
        Assert.Equal("void Interceptors.Nested.D.Interceptor1()", interceptor.ToTestDisplayString());
    }

    [Fact]
    public void GetInterceptorMethod_14()
    {
        // Interceptor is in a parent of the expected namespace. Not discovered.
        var source = ("""
            C.M();

            class C
            {
                public static void M() => throw null;
            }
            """, "Program.cs");

        var locations = GetInterceptableLocations(source);
        var interceptorSource = ($$"""
            using System;
            using System.Runtime.CompilerServices;

            namespace Interceptors
            {
                public static class D
                {
                    [InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
                    public static void Interceptor1() => Console.Write(1);
                }
            }
            """, "Interceptor.cs");

        var comp = CreateCompilation([source, interceptorSource, s_attributesSource], parseOptions: TestOptions.Regular.WithFeature("InterceptorsNamespaces", "Interceptors.Nested"));

        var tree = comp.SyntaxTrees[0];
        var model = comp.GetSemanticModel(tree);
        var call = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();

        Assert.Null(model.GetInterceptorMethod(call));

        comp.VerifyEmitDiagnostics(
            // Interceptor.cs(8,10): error CS9137: The 'interceptors' feature is not enabled in this namespace. Add '<InterceptorsNamespaces>$(InterceptorsNamespaces);Interceptors</InterceptorsNamespaces>' to your project.
            //         [InterceptsLocation(1, "NnwjYrJAcjZ/s0+OSiPXqwIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorsFeatureNotEnabled, "InterceptsLocation").WithArguments("<InterceptorsNamespaces>$(InterceptorsNamespaces);Interceptors</InterceptorsNamespaces>").WithLocation(8, 10));

        Assert.Null(model.GetInterceptorMethod(call));
    }

    // https://github.com/dotnet/roslyn/issues/72265
    // As part of the work to drop support for file path based interceptors, a significant number of existing tests here will need to be ported to checksum-based.

    [Fact]
    public void Checksum_01()
    {
        var source = CSharpTestSource.Parse("""
            class C
            {
                static void M() => throw null!;

                static void Main()
                {
                    M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)], expectedOutput: "1");
        verifier.VerifyDiagnostics();

        // again, but using the accessors for specifically retrieving the individual attribute arguments
        interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation({{locationSpecifier!.Version}}, "{{locationSpecifier.Data}}")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        verifier = CompileAndVerify([source, interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)], expectedOutput: "1");
        verifier.VerifyDiagnostics();
    }

    [Fact]
    public void Checksum_02()
    {
        var tree = CSharpTestSource.Parse("""
            class C
            {
                static void M() => throw null!;

                static void Main()
                {
                    M();
                    M();
                }
            }
            """.NormalizeLineEndings(), "path/to/Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(tree);
        var model = comp.GetSemanticModel(tree);
        if (tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().ToList() is not [var node, var otherNode])
        {
            throw ExceptionUtilities.Unreachable();
        }

        var locationSpecifier = model.GetInterceptableLocation(node);
        Assert.False(locationSpecifier!.Equals(null));

        // Verify behaviors of the public APIs.
        Assert.Equal("path/to/Program.cs(7,9)", locationSpecifier!.GetDisplayLocation());
        Assert.Equal(1, locationSpecifier.Version);
        Assert.Equal(locationSpecifier, locationSpecifier);

        Assert.NotSame(locationSpecifier, model.GetInterceptableLocation(node));
        Assert.Equal(locationSpecifier, model.GetInterceptableLocation(node));
        Assert.Equal(locationSpecifier.GetHashCode(), model.GetInterceptableLocation(node)!.GetHashCode());

        // If Data changes it might be the case that 'SourceText.GetContentHash()' has changed algorithms.
        // In this case we need to adjust the SourceMethodSymbol.DecodeInterceptsLocationAttribute impl to remain compatible with v1 and consider introducing a v2 which uses the new content hash algorithm.
        AssertEx.Equal("xRCCFCvTOZMORzSr/fZQFlIAAABQcm9ncmFtLmNz", locationSpecifier.Data);
        AssertEx.Equal("""[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "xRCCFCvTOZMORzSr/fZQFlIAAABQcm9ncmFtLmNz")]""", locationSpecifier.GetInterceptsLocationAttributeSyntax());

        var otherLocation = model.GetInterceptableLocation(otherNode)!;
        Assert.NotEqual(locationSpecifier, otherLocation);
        // While it is not incorrect for the HashCodes of these instances to be equal, we don't expect it in this case.
        Assert.NotEqual(locationSpecifier.GetHashCode(), otherLocation.GetHashCode());

        Assert.Equal("path/to/Program.cs(8,9)", otherLocation.GetDisplayLocation());
        AssertEx.Equal("xRCCFCvTOZMORzSr/fZQFmAAAABQcm9ncmFtLmNz", otherLocation.Data);
        AssertEx.Equal("""[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "xRCCFCvTOZMORzSr/fZQFmAAAABQcm9ncmFtLmNz")]""", otherLocation.GetInterceptsLocationAttributeSyntax());

    }

    [Fact]
    public void Checksum_03()
    {
        // Invalid base64
        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation(1, "jB4qgCy292LkEGCwmD+R6AcAAAAJAAAAUHJvZ3JhbS5jcw===")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp = CreateCompilation([interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9231: The data argument to InterceptsLocationAttribute is not in the correct format.
            //     [InterceptsLocation(1, "jB4qgCy292LkEGCwmD+R6AcAAAAJAAAAUHJvZ3JhbS5jcw===")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidFormat, "InterceptsLocation").WithLocation(6, 6));
    }

    [Fact]
    public void Checksum_04()
    {
        // Test invalid UTF-8 encoded to base64

        var builder = new BlobBuilder();
        // all zeros checksum and zero position
        builder.WriteBytes(value: 0, byteCount: 20);

        // write invalid utf-8
        builder.WriteByte(0xc0);

        var base64 = Convert.ToBase64String(builder.ToArray());

        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation(1, "{{base64}}")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp = CreateCompilation([interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9231: The data argument to InterceptsLocationAttribute is not in the correct format.
            //     [InterceptsLocation(1, "AAAAAAAAAAAAAAAAAAAAAAAAAADA")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidFormat, "InterceptsLocation").WithLocation(6, 6));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AA==")]
    public void Checksum_05(string data)
    {
        // Test data value too small
        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation(1, "{{data}}")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp = CreateCompilation([interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9231: The data argument to InterceptsLocationAttribute is not in the correct format.
            //     [InterceptsLocation(1, "")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidFormat, "InterceptsLocation").WithLocation(6, 6));
    }

    [Fact]
    public void Checksum_06()
    {
        // Null data
        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation(1, null)]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp = CreateCompilation([interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9231: The data argument to InterceptsLocationAttribute is not in the correct format.
            //     [InterceptsLocation(1, null)]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDataInvalidFormat, "InterceptsLocation").WithLocation(6, 6));
    }

    [Fact]
    public void Checksum_07()
    {
        // File not found

        var source = CSharpTestSource.Parse("""
            class C
            {
                static void M() => throw null!;

                static void Main()
                {
                    M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation({{locationSpecifier.Version}}, "{{locationSpecifier.Data}}")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp1 = CreateCompilation([interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp1.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9234: Cannot intercept a call in file 'Program.cs' because a matching file was not found in the compilation.
            //     [InterceptsLocation(1, "jB4qgCy292LkEGCwmD+R6FIAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationFileNotFound, "InterceptsLocation").WithArguments("Program.cs").WithLocation(6, 6));
    }

    [Fact]
    public void Checksum_08()
    {
        // Duplicate file

        var source = """
            class C
            {
                static void M() => throw null!;

                static void Main()
                {
                    M();
                }
            }
            """;
        var sourceTree1 = CSharpTestSource.Parse(source, path: "Program1.cs", options: RegularWithInterceptors);

        var comp = CreateCompilation(sourceTree1);
        var model = comp.GetSemanticModel(sourceTree1);
        var node = sourceTree1.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation({{locationSpecifier.Version}}, "{{locationSpecifier.Data}}")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp1 = CreateCompilation([
            sourceTree1,
            CSharpTestSource.Parse(source, path: "Program2.cs", options: RegularWithInterceptors),
            interceptors,
            CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp1.GetDiagnostics().Where(d => d.Location.SourceTree == interceptors).Verify(
            // Interceptors.cs(6,6): error CS9233: Cannot intercept a call in file 'Program1.cs' because it is duplicated elsewhere in the compilation.
            //     [InterceptsLocation(1, "jB4qgCy292LkEGCwmD+R6FIAAABQcm9ncmFtMS5jcw==")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationDuplicateFile, "InterceptsLocation").WithArguments("Program1.cs").WithLocation(6, 6));
    }

    [Fact]
    public void Checksum_09()
    {
        // Call can be intercepted syntactically but a semantic error occurs when actually performing it.

        var source = CSharpTestSource.Parse("""
            using System;

            class C
            {
                static Action P { get; } = null!;

                static void Main()
                {
                    P();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void P1(this C c) => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(5,6): error CS9207: Cannot intercept 'P' because it is not an invocation of an ordinary member method.
            //     [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "ZnP1PXDK5WDD07FTErR9eWUAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptableMethodMustBeOrdinary, "global::System.Runtime.CompilerServices.InterceptsLocationAttribute").WithArguments("P").WithLocation(5, 6));
    }

    [Fact]
    public void Checksum_10()
    {
        // Call cannot be intercepted syntactically

        var source = CSharpTestSource.Parse("""
            using System;

            static class C
            {
                public static void M(this object obj) => throw null!;

                static void Main()
                {
                    null();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        comp.VerifyEmitDiagnostics(
            // Program.cs(9,9): error CS0149: Method name expected
            //         null();
            Diagnostic(ErrorCode.ERR_MethodNameExpected, "null").WithLocation(9, 9));

        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node);
        Assert.Null(locationSpecifier);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(9999)]
    public void Checksum_11(int version)
    {
        // Bad version
        var interceptors = CSharpTestSource.Parse($$"""
            using System;
            using System.Runtime.CompilerServices;

            static class Interceptors
            {
                [InterceptsLocation({{version}}, "jB4qgCy292LkEGCwmD+R6AcAAAAJAAAAUHJvZ3JhbS5jcw===")]
                public static void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var comp = CreateCompilation([interceptors, CSharpTestSource.Parse(s_attributesSource.text, s_attributesSource.path, RegularWithInterceptors)]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9232: Version '0' of the interceptors format is not supported. The latest supported version is '1'.
            //     [InterceptsLocation(0, "jB4qgCy292LkEGCwmD+R6AcAAAAJAAAAUHJvZ3JhbS5jcw===")]
            Diagnostic(ErrorCode.ERR_InterceptsLocationUnsupportedVersion, "InterceptsLocation").WithArguments($"{version}").WithLocation(6, 6));
    }

    [Fact]
    public void Checksum_12()
    {
        // Attempt to insert null paths into InterceptableLocation.

        var tree = CSharpTestSource.Parse("""
            class C
            {
                static void M() => throw null!;

                static void Main()
                {
                    M();
                }
            }
            """.NormalizeLineEndings(), path: null, RegularWithInterceptors);
        Assert.Equal("", tree.FilePath);

        var comp = CreateCompilation(tree);
        var model = comp.GetSemanticModel(tree);
        var node = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;
        Assert.Equal("(7,9)", locationSpecifier.GetDisplayLocation());
        AssertEx.Equal("""[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "jB4qgCy292LkEGCwmD+R6FIAAAA=")]""", locationSpecifier.GetInterceptsLocationAttributeSyntax());
    }

    [Fact]
    public void ConditionalAccess_ReferenceType_01()
    {
        // Conditional access on a non-null value
        var source = CSharpTestSource.Parse("""
            class C
            {
                void M() => throw null!;

                static void Main()
                {
                    var c = new C();
                    c?.M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            #nullable enable
            using System;

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this C c) => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void Interceptors.M1(this C c)", method.ToTestDisplayString());
    }

    [Fact]
    public void ConditionalAccess_ReferenceType_02()
    {
        // Conditional access on a null value
        var source = CSharpTestSource.Parse("""
            #nullable enable
            using System;

            class C
            {
                void M() => throw null!;

                static void Main()
                {
                    C? c = null;
                    c?.M();
                    Console.Write(1);
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            #nullable enable

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this C c) => throw null!;
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void Interceptors.M1(this C c)", method.ToTestDisplayString());
    }

    [Fact]
    public void ConditionalAccess_NotAnInvocation()
    {
        // use a location specifier which refers to a conditional access that is not being invoked.
        var source = CSharpTestSource.Parse("""
            class C
            {
                int P => throw null!;

                static void Main()
                {
                    var c = new C();
                    _ = c?.P;
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = (CSharpSemanticModel)comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<MemberBindingExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocationInternal(node.Name, cancellationToken: default)!;

        var interceptors = CSharpTestSource.Parse($$"""
            #nullable enable
            using System;

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this C c) => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, s_attributesTree]);
        comp.VerifyEmitDiagnostics(
            // Interceptors.cs(6,6): error CS9151: Possible method name 'P' cannot be intercepted because it is not being invoked.
            //     [global::System.Runtime.CompilerServices.InterceptsLocationAttribute(1, "q2jDXUSFcU71GJHh7313cHEAAABQcm9ncmFtLmNz")]
            Diagnostic(ErrorCode.ERR_InterceptorNameNotInvoked, "global::System.Runtime.CompilerServices.InterceptsLocationAttribute").WithArguments("P").WithLocation(6, 6));
    }

    [Fact]
    public void ConditionalAccess_ValueType_01()
    {
        // Conditional access on a nullable value type with a non-null value
        // Note that we can't intercept a conditional-access with an extension due to https://github.com/dotnet/roslyn/issues/71657
        var source = CSharpTestSource.Parse("""
            partial struct S
            {
                void M() => throw null!;

                static void Main()
                {
                    S? s = new S();
                    s?.M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            partial struct S
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public void M1() => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void S.M1()", method.ToTestDisplayString());
    }

    [Fact]
    public void ConditionalAccess_ValueType_02()
    {
        // Conditional access on a nullable value type with a null value
        var source = CSharpTestSource.Parse("""
            using System;

            partial struct S
            {
                void M() => throw null!;

                static void Main()
                {
                    S? s = null;
                    s?.M();
                    Console.Write(1);
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            partial struct S
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public void M1() => throw null!;
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void S.M1()", method.ToTestDisplayString());
    }

    [Theory]
    [InlineData("p->M();")]
    [InlineData("(*p).M();")]
    public void PointerAccess_01(string invocation)
    {
        var source = CSharpTestSource.Parse($$"""
            struct S
            {
                void M() => throw null!;

                static unsafe void Main()
                {
                    S s = default;
                    S* p = &s;
                    {{invocation}}
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source, options: TestOptions.UnsafeDebugExe);
        CompileAndVerify(comp, verify: Verification.Fails);

        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            #nullable enable
            using System;

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this ref S s) => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify(
            [source, interceptors, s_attributesTree],
            options: TestOptions.UnsafeDebugExe,
            verify: Verification.Fails,
            expectedOutput: "1");
        verifier.VerifyDiagnostics();

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void Interceptors.M1(this ref S s)", method.ToTestDisplayString());
    }

    [Theory]
    [CombinatorialData]
    public void PointerAccess_02([CombinatorialValues("p->M();", "(*p).M();")] string invocation, [CombinatorialValues("", "ref ")] string refKind)
    {
        // Original method is an extension
        var source = CSharpTestSource.Parse($$"""
            struct S
            {
                static unsafe void Main()
                {
                    S s = default;
                    S* p = &s;
                    {{invocation}}
                }
            }

            static class Ext
            {
                public static void M(this {{refKind}}S s) => throw null!;
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source, options: TestOptions.UnsafeDebugExe);
        CompileAndVerify(comp, verify: Verification.Fails);

        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            #nullable enable
            using System;

            static class Interceptors
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this {{refKind}}S s) => Console.Write(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify(
            [source, interceptors, s_attributesTree],
            options: TestOptions.UnsafeDebugExe,
            verify: Verification.Fails,
            expectedOutput: "1");
        verifier.VerifyDiagnostics();

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal($"void Interceptors.M1(this {refKind}S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void ReceiverCapturedToTemp_StructRvalueReceiver()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            public struct S
            {
                void M() => Console.WriteLine(0);

                public static void Main()
                {
                    new S().M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            public static class C
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this ref S s) => Console.WriteLine(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("S.Main", """
            {
              // Code size       16 (0x10)
              .maxstack  1
              .locals init (S V_0)
              IL_0000:  ldloca.s   V_0
              IL_0002:  initobj    "S"
              IL_0008:  ldloca.s   V_0
              IL_000a:  call       "void C.M1(ref S)"
              IL_000f:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void C.M1(this ref S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void ReceiverCapturedToTemp_StructInReceiver()
    {
        // Implicitly capture receiver to temp in 's.M()' because target method needs a writable reference.
        var source = CSharpTestSource.Parse("""
            using System;

            public struct S
            {
                void M() => Console.WriteLine(0);

                public static void Main()
                {
                    M0(new S());
                }

                static void M0(in S s)
                {
                    s.M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            public static class C
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this ref S s) => Console.WriteLine(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("S.M0", """
            {
              // Code size       15 (0xf)
              .maxstack  1
              .locals init (S V_0)
              IL_0000:  ldarg.0
              IL_0001:  ldobj      "S"
              IL_0006:  stloc.0
              IL_0007:  ldloca.s   V_0
              IL_0009:  call       "void C.M1(ref S)"
              IL_000e:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void C.M1(this ref S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void ReceiverNotCapturedToTemp_StructRefReceiver()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            public struct S
            {
                void M() => Console.WriteLine(0);

                public static void Main()
                {
                    S s = default;
                    M0(ref s);
                }

                static void M0(ref S s)
                {
                    s.M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            public static class C
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this ref S s) => Console.WriteLine(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("S.M0", """
            {
              // Code size        7 (0x7)
              .maxstack  1
              IL_0000:  ldarg.0
              IL_0001:  call       "void C.M1(ref S)"
              IL_0006:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void C.M1(this ref S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void ReceiverNotCapturedToTemp_StructReadonlyMethod()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            public struct S
            {
                readonly void M() => Console.WriteLine(0);

                public static void Main()
                {
                    M0(new S());
                }

                static void M0(in S s)
                {
                    s.M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            public static class C
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this in S s) => Console.WriteLine(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("S.M0", """
            {
              // Code size        7 (0x7)
              .maxstack  1
              IL_0000:  ldarg.0
              IL_0001:  call       "void C.M1(in S)"
              IL_0006:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void C.M1(this in S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void ReceiverNotCapturedToTemp_StructLvalueReceiver()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            public struct S
            {
                void M() => Console.WriteLine(0);

                public static void Main()
                {
                    M0(new S());
                }

                static void M0(S s)
                {
                    s.M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            public static class C
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this ref S s) => Console.WriteLine(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("S.M0", """
            {
              // Code size        8 (0x8)
              .maxstack  1
              IL_0000:  ldarga.s   V_0
              IL_0002:  call       "void C.M1(ref S)"
              IL_0007:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void C.M1(this ref S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void ReceiverNotCapturedToTemp_ByValueParameter()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            public class C
            {
                void M() => Console.WriteLine(0);

                public static void Main()
                {
                    new C().M();
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            public static class SC
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this C c) => Console.WriteLine(1);
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "1");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("C.Main", """
            {
              // Code size       11 (0xb)
              .maxstack  1
              IL_0000:  newobj     "C..ctor()"
              IL_0005:  call       "void SC.M1(C)"
              IL_000a:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void SC.M1(this C c)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void Receiver_RefReturn_NotCapturedToTemp()
    {
        var source = CSharpTestSource.Parse("""
            using System;

            public struct S
            {
                public int F;

                static S s;
                static ref S RS() => ref s;

                void M() => throw null!;

                public static void Main()
                {
                    RS().F = 1;
                    Console.Write(RS().F);
                    RS().M();
                    Console.Write(RS().F);
                }
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source);
        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single(i => i.ToString() == "RS().M()");
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            public static class SC
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static void M1(this ref S s) => s.F = 2;
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        var verifier = CompileAndVerify([source, interceptors, s_attributesTree], expectedOutput: "12");
        verifier.VerifyDiagnostics();
        verifier.VerifyIL("S.Main", """
            {
              // Code size       52 (0x34)
              .maxstack  2
              IL_0000:  call       "ref S S.RS()"
              IL_0005:  ldc.i4.1
              IL_0006:  stfld      "int S.F"
              IL_000b:  call       "ref S S.RS()"
              IL_0010:  ldfld      "int S.F"
              IL_0015:  call       "void System.Console.Write(int)"
              IL_001a:  call       "ref S S.RS()"
              IL_001f:  call       "void SC.M1(ref S)"
              IL_0024:  call       "ref S S.RS()"
              IL_0029:  ldfld      "int S.F"
              IL_002e:  call       "void System.Console.Write(int)"
              IL_0033:  ret
            }
            """);

        comp = (CSharpCompilation)verifier.Compilation;
        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        Assert.Equal("void SC.M1(this ref S s)", method.ToTestDisplayString());
    }

    [Fact, WorkItem("https://github.com/dotnet/roslyn/issues/71657")]
    public void CannotReturnRefToImplicitTemp()
    {
        var source = CSharpTestSource.Parse("""
            using System;
            using System.Diagnostics.CodeAnalysis;

            public ref struct S
            {
                static Span<int> Test()
                {
                    return new S().M();
                }

                [UnscopedRef]
                public Span<int> M() => default;
            }
            """, "Program.cs", RegularWithInterceptors);

        var comp = CreateCompilation(source, targetFramework: TargetFramework.Net90);
        comp.VerifyEmitDiagnostics(
            // Program.cs(8,16): error CS8156: An expression cannot be used in this context because it may not be passed or returned by reference
            //         return new S().M();
            Diagnostic(ErrorCode.ERR_RefReturnLvalueExpected, "new S()").WithLocation(8, 16));

        var model = comp.GetSemanticModel(source);
        var node = source.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Single(i => i.ToString() == "new S().M()");
        var locationSpecifier = model.GetInterceptableLocation(node)!;

        var interceptors = CSharpTestSource.Parse($$"""
            using System;

            static class D
            {
                {{locationSpecifier.GetInterceptsLocationAttributeSyntax()}}
                public static Span<int> M(this ref S s) => default;
            }
            """, "Interceptors.cs", RegularWithInterceptors);

        comp = CreateCompilation([source, interceptors, s_attributesTree], targetFramework: TargetFramework.Net90);
        comp.VerifyEmitDiagnostics(
            // Program.cs(8,16): error CS8156: An expression cannot be used in this context because it may not be passed or returned by reference
            //         return new S().M();
            Diagnostic(ErrorCode.ERR_RefReturnLvalueExpected, "new S()").WithLocation(8, 16));

        model = comp.GetSemanticModel(source);
        var method = model.GetInterceptorMethod(node);
        AssertEx.Equal("System.Span<System.Int32> D.M(this ref S s)", method.ToTestDisplayString());
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_01()
    {
        // original calls use extensions, interceptors are classic
        var source = """
object.M();
" ran".M2();

static class E
{
    extension(object o)
    {
        public static void M() => throw null;
        public void M2() => throw null;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method() { System.Console.Write(42); }

    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
    public static void Method2(this object o) { System.Console.Write(o); }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "42 ran").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_02()
    {
        // top-level difference in receiver parameter nullability
        var source = """
#nullable enable

new object().M();

static class E
{
    extension(object o)
    {
        public void M() => throw null!;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
#nullable enable

static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method(this object? o) { System.Console.Write("ran"); }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_03()
    {
        // nested difference in receiver parameter nullability
        var source = """
#nullable enable

new C<object>().M();

static class E
{
    extension(C<object> o)
    {
        public void M() => throw null!;
    }
}

class C<T> { }
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
#nullable enable

static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method(this C<object?> o) { }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'C<object> o' on 'E.extension(C<object>).M()'.
            //     [System.Runtime.CompilerServices.InterceptsLocation(1, "T81R8uSCzQRaZ7VAf0D7uCQAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("C<object> o", "E.extension(C<object>).M()").WithLocation(5, 6));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_04()
    {
        // refness difference in receiver parameter
        var source = """
42.M();

static class E
{
    extension(int i)
    {
        public void M() => throw null!;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method(ref this int i) { }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (3,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'int i' on 'E.extension(int).M()'.
            //     [System.Runtime.CompilerServices.InterceptsLocation(1, "JjM6W8JDDhaVDDV4fi7OigMAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("int i", "E.extension(int).M()").WithLocation(3, 6));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_05()
    {
        var source = """
42.M();

static class E
{
    extension(int i)
    {
        public void M() => throw null!;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method() { }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (3,6): error CS9148: Interceptor must have a 'this' parameter matching parameter 'int i' on 'E.extension(int).M()'.
            //     [System.Runtime.CompilerServices.InterceptsLocation(1, "JjM6W8JDDhaVDDV4fi7OigMAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("int i", "E.extension(int).M()").WithLocation(3, 6));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_06()
    {
        var source = """
42.M();

static class E
{
    extension(int i)
    {
        public void M() => throw null!;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method(int i) { }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (3,6): error CS9144: Cannot intercept method 'E.extension(int).M()' with interceptor 'Interceptors.Method(int)' because the signatures do not match.
            //     [System.Runtime.CompilerServices.InterceptsLocation(1, "JjM6W8JDDhaVDDV4fi7OigMAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("E.extension(int).M()", "Interceptors.Method(int)").WithLocation(3, 6));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_07()
    {
        // original calls use static extension, interceptors with and without `this`
        var source = """
int.M(42);
int.M(43);

static class E
{
    extension(int)
    {
        public static void M(int i) => throw null!;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method(int i) { System.Console.Write(i); }

    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
    public static void Method2(this int i) { System.Console.Write(i); }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "4243").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_09()
    {
        // original calls use non-extension invocations, interceptor is an extension method
        var source = """
C.M();
new C().M2();

class C
{
    public static void M() => throw null!;
    public void M2() => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(C c)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public static void Method() { System.Console.Write("ran "); }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
        public void Method2() { System.Console.Write(c); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran C").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_10()
    {
        // original call uses non-extension instance invocations, interceptor is a static extension method
        var source = """
new C().M2();

class C
{
    public void M2() => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(C)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public static void Method(C c) { }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,10): error CS9144: Cannot intercept method 'C.M2()' with interceptor 'Interceptors.Method(C)' because the signatures do not match.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "6ii77DXfMpjahn56tJIXdAgAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("C.M2()", "Interceptors.Method(C)").WithLocation(5, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_11()
    {
        // original call uses classic extension invocations, interceptor is a new extension method
        var source = """
new object().M();

public static class Extensions
{
    public static void M(this object o) => throw null;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method() { System.Console.Write("ran"); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();

        interceptors = $$"""
static class Interceptors
{
    extension(object)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public static void Method(object o) { }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,10): error CS9148: Interceptor must have a 'this' parameter matching parameter 'object o' on 'Extensions.M(object)'.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "rthnOf6S6aLCQ1g5K5pDgA0AAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorMustHaveMatchingThisParameter, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("object o", "Extensions.M(object)").WithLocation(5, 10));

        interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method(this object o) { System.Console.Write("ran"); }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_12()
    {
        // original call uses new extension invocations, interceptors are new extension methods
        var source = """
object.M();
new object().M2();

public static class Extensions
{
    extension(object o)
    {
        public static void M() => throw null;
        public void M2() => throw null;
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public static void Method() { System.Console.Write("ran1 "); }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
        public void Method2() { System.Console.Write("ran2"); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran1 ran2").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_13()
    {
        // interception within extension body
        var source = """
public static class E
{
    extension(int i)
    {
        public void M()
        {
            i.ToString();
        }

        public string P => i.ToString();

        public static void M2()
        {
            42.ToString();
        }

        public static string P2 => 43.ToString();
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
10.M();
_ = 11.P;
int.M2();
_ = int.P2;

static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static string Method1(this ref int o) { System.Console.Write("ran1 "); return ""; }

    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
    public static string Method2(this ref int i) { System.Console.Write("ran2 "); return ""; }

    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
    public static string Method3(this ref int i) { System.Console.Write("ran3 "); return ""; }

    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
    public static string Method4(this ref int i) { System.Console.Write("ran4 "); return ""; }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran1 ran2 ran3 ran4").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_14()
    {
        // mismatch in return types
        var source = """
public static class E
{
    extension(int i)
    {
        public void M()
        {
            i.ToString();
        }
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
    public static void Method1(this ref int o) { }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        // Consider printing the return types as part of the compared signatures with FormattedSymbol
        comp.VerifyEmitDiagnostics(
            // (3,6): error CS9144: Cannot intercept method 'int.ToString()' with interceptor 'Interceptors.Method1(ref int)' because the signatures do not match.
            //     [System.Runtime.CompilerServices.InterceptsLocation(1, "24Q46HTnfKKGDA49FINUx2kAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("int.ToString()", "Interceptors.Method1(ref int)").WithLocation(3, 6));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_15()
    {
        var source = """
object.M(new object());

public static class E
{
    extension(object)
    {
        public static void M(object o) { }
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method(object o2) { }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (5,10): error CS9144: Cannot intercept method 'E.extension(object).M(object)' with interceptor 'Interceptors.Method(object, object)' because the signatures do not match.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "CtYO/tuDG+qIspclOkHaEwcAAAA=")]
            Diagnostic(ErrorCode.ERR_InterceptorSignatureMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("E.extension(object).M(object)", "Interceptors.Method(object, object)").WithLocation(5, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_16()
    {
        // receiver initially isn't converted (would be handled as constrained call) but gets converted for interceptor
        var source = """
new S().ToString();

struct S { }
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(System.ValueType v)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public string Method() { System.Console.Write("ran"); return ""; }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_17()
    {
        // Implicitly capture receiver to temp in 's.M()' because target method needs a writable reference.
        var source = """
S.M0(new S());

public struct S
{
    void M() => System.Console.WriteLine(0);

    public static void M0(in S s)
    {
        s.M();
    }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(ref S s)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
        public void Method() => System.Console.WriteLine("ran");
    }
}
""";
        var verifier = CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();
        verifier.VerifyIL("S.M0", """
{
  // Code size       15 (0xf)
  .maxstack  1
  .locals init (S V_0)
  IL_0000:  ldarg.0
  IL_0001:  ldobj      "S"
  IL_0006:  stloc.0
  IL_0007:  ldloca.s   V_0
  IL_0009:  call       "void Interceptors.Method(ref S)"
  IL_000e:  ret
}
""");
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_18()
    {
        // Nullability difference in return
        var source = """
#nullable enable

new object().M();
new object().M();
new object().M();

new object().M2();
new object().M2();
new object().M2();

new object().M3();
new object().M3();
new object().M3();

static class E
{
    public static string M(this object o) => throw null!;
    public static string? M2(this object o) => throw null!;
#nullable disable
    public static string M3(this object o) => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
#nullable enable

static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public string Method0() { System.Console.Write("ran0 "); return ""; }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 1
        public string? Method1() { System.Console.Write("ran1 "); return ""; }

#nullable disable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
        public string Method2() { System.Console.Write("ran2 "); return ""; }

#nullable enable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
        public string Method3() { System.Console.Write("ran3 "); return ""; }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[4]!)}})]
        public string? Method4() { System.Console.Write("ran4 "); return ""; }

#nullable disable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[5]!)}})]
        public string Method5() { System.Console.Write("ran5 "); return ""; }

#nullable enable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[6]!)}})]
        public string Method6() { System.Console.Write("ran6 "); return ""; }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[7]!)}})]
        public string? Method7() { System.Console.Write("ran7 "); return ""; }

#nullable disable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[8]!)}})]
        public string Method8() { System.Console.Write("ran8 "); return ""; }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        CompileAndVerify(comp, expectedOutput: "ran0 ran1 ran2 ran3 ran4 ran5 ran6 ran7 ran8").VerifyDiagnostics(
            // (10,10): warning CS9158: Nullability of reference types in return type doesn't match interceptable method 'E.M(object)'.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "kyzJ78tfBnC7NCAmEETPGDQAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInReturnTypeOnInterceptor, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("E.M(object)").WithLocation(10, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_19()
    {
        // Nullability difference in parameter
        var source = """
#nullable enable

new object().M("");
new object().M("");
new object().M("");

new object().M2("");
new object().M2("");
new object().M2("");

new object().M3("");
new object().M3("");
new object().M3("");

static class E
{
    public static void M(this object o, string s) => throw null!;
    public static void M2(this object o, string? s) => throw null!;
#nullable disable
    public static void M3(this object o, string s) => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
#nullable enable

static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method0(string s) { System.Console.Write("ran0 "); }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
        public void Method1(string? s) { System.Console.Write("ran1 "); }

#nullable disable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
        public void Method2(string s) { System.Console.Write("ran2 "); }

#nullable enable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})] // 1
        public void Method3(string s) { System.Console.Write("ran3 "); }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[4]!)}})]
        public void Method4(string? s) { System.Console.Write("ran4 "); }

#nullable disable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[5]!)}})]
        public void Method5(string s) { System.Console.Write("ran5 "); }

#nullable enable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[6]!)}})]
        public void Method6(string s) { System.Console.Write("ran6 "); }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[7]!)}})]
        public void Method7(string? s) { System.Console.Write("ran7 "); }

#nullable disable
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[8]!)}})]
        public void Method8(string s) { System.Console.Write("ran8 "); }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);

        CompileAndVerify(comp, expectedOutput: "ran0 ran1 ran2 ran3 ran4 ran5 ran6 ran7 ran8").VerifyDiagnostics(
            // (18,10): warning CS9159: Nullability of reference types in type of parameter 's' doesn't match interceptable method 'E.M2(object, string?)'.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "+VOLrIHFLh9ndZzTPFZ19mIAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInParameterTypeOnInterceptor, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("s", "E.M2(object, string?)").WithLocation(18, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_20()
    {
        // Nullability of receiver, original is extension method
        var source = """
#nullable enable

object? oNull = null;
oNull.M(); // 1

object? oNull2 = null;
oNull2.M();

object? oNotNull = new object();
oNotNull.M();
oNotNull.M();

static class E
{
    public static void M(this object? o) => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
#nullable enable

static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})] // 1
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})] // 2
        public void Method0() { System.Console.Write("ran0 "); }
    }

    extension(object? o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
        public void Method1() { System.Console.Write("ran1 "); }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);

        CompileAndVerify(comp, expectedOutput: "ran0 ran1 ran0 ran1").VerifyDiagnostics(
            // (7,10): warning CS9159: Nullability of reference types in type of parameter 'o' doesn't match interceptable method 'E.M(object?)'.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "UOx514BZZQx0rQJnlTZlGTEAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInParameterTypeOnInterceptor, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("o", "E.M(object?)").WithLocation(7, 10),
            // (8,10): warning CS9159: Nullability of reference types in type of parameter 'o' doesn't match interceptable method 'E.M(object?)'.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "UOx514BZZQx0rQJnlTZlGZAAAAA=")] // 2
            Diagnostic(ErrorCode.WRN_NullabilityMismatchInParameterTypeOnInterceptor, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("o", "E.M(object?)").WithLocation(8, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_21()
    {
        // Nullability of receiver, original is instance method
        var source = """
#nullable enable

object? oNull = null;
oNull.ToString(); // 1

object? oNull2 = null;
oNull2.ToString(); // 2

object? oNotNull = new object();
oNotNull.ToString();
oNotNull.ToString();

object? oNull3 = null;
oNull3.ToString(); // 3
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
#nullable enable

static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
        public string Method0() { System.Console.Write("ran0 "); return ""; }
    }

    extension(object? o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
        public string Method1() { System.Console.Write("ran1 "); return ""; }
    }

    [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[4]!)}})]
    public static string Method2(this object o) { System.Console.Write("ran2 "); return ""; }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);

        CompileAndVerify(comp, expectedOutput: "ran0 ran1 ran0 ran1 ran2").VerifyDiagnostics(
            // (4,1): warning CS8602: Dereference of a possibly null reference.
            // oNull.ToString(); // 1
            Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "oNull").WithLocation(4, 1),
            // (7,1): warning CS8602: Dereference of a possibly null reference.
            // oNull2.ToString(); // 2
            Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "oNull2").WithLocation(7, 1),
            // (14,1): warning CS8602: Dereference of a possibly null reference.
            // oNull3.ToString(); // 3
            Diagnostic(ErrorCode.WRN_NullReferenceReceiver, "oNull3").WithLocation(14, 1));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_22()
    {
        // Tuple names difference in return
        var source = """
new object().M();
new object().M();

static class E
{
    public static (object a, object b) M(this object o) => throw null!;
    public static (object a, object b) M2(this object o) => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public (object a, object b) Method0() { System.Console.Write("ran0 "); return (null!, null!); }

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 1
        public (object other1, object other2) Method1() { System.Console.Write("ran1 "); return (null!, null!); }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);

        CompileAndVerify(comp, expectedOutput: "ran0 ran1").VerifyDiagnostics(
            // (8,10): warning CS9154: Intercepting a call to 'E.M(object)' with interceptor 'Interceptors.Method1(object)', but the signatures do not match.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "ZhBCDQ5cTiMfojQyN7NYNyAAAAA=")] // 1
            Diagnostic(ErrorCode.WRN_InterceptorSignatureMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("E.M(object)", "Interceptors.Method1(object)").WithLocation(8, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_23()
    {
        // scoped difference in parameter, original is extension method
        var source = """
RS s = new RS();
new object().M(ref s);
new object().M(ref s);

new object().M2(ref s);
new object().M2(ref s);

static class E
{
    public static void M(this object o, scoped ref RS s) => throw null!;
    public static void M2(this object o, ref RS s) => throw null!;
}

public ref struct RS { }
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
        public void Method0(scoped ref RS s) => throw null!;

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 1
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
        public void Method1(ref RS s) => throw null!;
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (9,10): error CS9156: Cannot intercept call to 'E.M(object, scoped ref RS)' with 'Interceptors.Method1(object, ref RS)' because of a difference in 'scoped' modifiers or '[UnscopedRef]' attributes.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "FcYsmv49zJYGD+zKGdRnfDcAAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorScopedMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("E.M(object, scoped ref RS)", "Interceptors.Method1(object, ref RS)").WithLocation(9, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_24()
    {
        // scoped difference in parameter, original is instance method
        var source = """
RS s = new RS();
new C().M(ref s);
new C().M(ref s);

new C().M2(ref s);
new C().M2(ref s);

public class C
{
    public void M(scoped ref RS s) => throw null!;
    public void M2(ref RS s) => throw null!;
}

public ref struct RS { }
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(C c)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[2]!)}})]
        public void Method0(scoped ref RS s) => throw null!;

        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})] // 1
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[3]!)}})]
        public void Method1(ref RS s) => throw null!;
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyEmitDiagnostics(
            // (9,10): error CS9156: Cannot intercept call to 'C.M(scoped ref RS)' with 'C.Method1(ref RS)' because of a difference in 'scoped' modifiers or '[UnscopedRef]' attributes.
            //         [System.Runtime.CompilerServices.InterceptsLocation(1, "WYzfSbWruDNNkIt11JOTKC0AAAA=")] // 1
            Diagnostic(ErrorCode.ERR_InterceptorScopedMismatch, "System.Runtime.CompilerServices.InterceptsLocation").WithArguments("C.M(scoped ref RS)", "C.Method1(ref RS)").WithLocation(9, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_25()
    {
        // scoped difference in receiver
        var source = """
RS s = new RS();
s.M();
s.M();

public ref struct RS
{
    public void M() => throw null!;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(ref RS s)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method0() { System.Console.Write("ran0 "); }
    }

    extension(scoped ref RS s)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[1]!)}})]
        public void Method1() { System.Console.Write("ran1 "); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran0 ran1").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_26()
    {
        // original call uses classic extension invocations, interceptor is a new extension method, there is explicit ref argument
        var source = """
int i = 0;
new object().M(ref i);

public static class Extensions
{
    public static void M(this object o, ref int i) => throw null;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(object o)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method(ref int i) { System.Console.Write("ran"); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_27()
    {
        // original call uses classic extension invocations, interceptor is a new extension method, there is an implicit ref on receiver and explicit ref argument
        var source = """
int i = 0;
int j = 42;
j.M(ref i);

public static class Extensions
{
    public static void M(this ref int j, ref int i) => throw null;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(ref int j)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method(ref int i) { System.Console.Write("ran"); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "ran").VerifyDiagnostics();
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_28()
    {
        // original call uses instance method, interceptor is a generic new extension method
        var source = """
new C<object>().M(42);

public class C<T>
{
    public void M<U>(U u) { }
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension<T>(C<T> t)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method<U>(U u) { System.Console.Write((typeof(T), typeof(U))); }
    }
}
""";
        CompileAndVerify([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "(System.Object, System.Int32)").VerifyDiagnostics();

        var interceptors2 = """
static class Interceptors
{
    extension<T>(C<T> t)
    {
        [System.Runtime.CompilerServices.InterceptsLocation("Program.cs", 1, 17)]
        public void Method<U>(U u) { System.Console.Write((typeof(T), typeof(U))); }
    }
}
""";
        CompileAndVerify([(source, "Program.cs"), interceptors2, s_attributesSource], parseOptions: RegularPreviewWithInterceptors, expectedOutput: "(System.Object, System.Int32)").VerifyDiagnostics(
            // (5,10): warning CS9270: 'InterceptsLocationAttribute(string, int, int)' is not supported. Move to 'InterceptableLocation'-based generation of these attributes instead. (https://github.com/dotnet/roslyn/issues/72133)
            //         [System.Runtime.CompilerServices.InterceptsLocation("Program.cs", 1, 17)]
            Diagnostic(ErrorCode.WRN_InterceptsLocationAttributeUnsupportedSignature, @"System.Runtime.CompilerServices.InterceptsLocation(""Program.cs"", 1, 17)").WithLocation(5, 10));
    }

    [Fact, CompilerTrait(CompilerFeature.Extensions)]
    public void Extensions_29()
    {
        // interceptor is a new extension method with no ExtensionParameter and no implementation method
        var source = """
new object().M();

public static class Extensions
{
    public static void M(this object o) => throw null;
}
""";
        var locations = GetInterceptableLocations(source);
        var interceptors = $$"""
static class Interceptors
{
    extension(__arglist)
    {
        [System.Runtime.CompilerServices.InterceptsLocation({{GetAttributeArgs(locations[0]!)}})]
        public void Method() { }
    }
}
""";
        var comp = CreateCompilation([source, interceptors, s_attributesSource], parseOptions: RegularPreviewWithInterceptors);
        comp.VerifyDiagnostics(
            // (3,15): error CS1669: __arglist is not valid in this context
            //     extension(__arglist)
            Diagnostic(ErrorCode.ERR_IllegalVarArgs, "__arglist").WithLocation(3, 15));
    }
}

