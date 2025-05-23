﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis.DocumentationComments;
using Microsoft.CodeAnalysis.Editor.CSharp.DocumentationComments;
using Microsoft.CodeAnalysis.Editor.UnitTests.CodeActions;
using Microsoft.CodeAnalysis.Editor.UnitTests.DocumentationComments;
using Microsoft.CodeAnalysis.Editor.UnitTests.Extensions;
using Microsoft.CodeAnalysis.Test.Utilities;
using Microsoft.VisualStudio.Commanding;
using Roslyn.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.Editor.CSharp.UnitTests.DocumentationComments;

[Trait(Traits.Feature, Traits.Features.DocumentationComments)]
public sealed class DocumentationCommentTests : AbstractDocumentationCommentTests
{
    [WpfFact]
    public void TypingCharacter_Class()
    {
        var code =
            """
            //$$
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_Record()
    {
        var code =
            """
            //$$
            record R;
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            record R;
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_RecordStruct()
    {
        var code =
            """
            //$$
            record struct R;
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            record struct R;
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_RecordWithPositionalParameters()
    {
        var code =
            """
            //$$
            record R(string S, int I);
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            record R(string S, int I);
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_ClassParameters()
    {
        var code =
            """
            //$$
            class R(string S, int I);
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            class R(string S, int I);
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_RecordStructWithPositionalParameters()
    {
        var code =
            """
            //$$
            record struct R(string S, int I);
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            record struct R(string S, int I);
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_StructParameters()
    {
        var code =
            """
            //$$
            struct R(string S, int I);
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            struct R(string S, int I);
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_Class_NewLine()
    {
        var code = """
            //$$
            class C
            {
            }
            """;

        var expected = """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected, newLine: """


            """);

        code = """
            //$$
            class C
            {
            }
            """;

        expected = """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected, newLine: """


            """);
    }

    [WpfFact]
    public void TypingCharacter_Class_AutoGenerateXmlDocCommentsOff()
    {
        var code =
            """
            //$$
            class C
            {
            }
            """;

        var expected =
            """
            ///$$
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected, globalOptions: new OptionsCollection(LanguageNames.CSharp)
        {
            { DocumentationCommentOptionsStorage.AutoXmlDocCommentGeneration, false }
        });
    }

    [WpfFact]
    public void TypingCharacter_Method()
    {
        var code =
            """
            class C
            {
                //$$
                int M<T>(int goo) { return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="goo"></param>
                /// <returns></returns>
                int M<T>(int goo) { return 0; }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/54245")]
    public void TypingCharacter_Method_WithExceptions()
    {
        var code =
            """
            class C
            {
                //$$
                int M<T>(int goo)
                {
                    if (goo < 0) throw new /*leading trivia*/Exception/*trailing trivia*/();
                    return 0;
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="goo"></param>
                /// <returns></returns>
                /// <exception cref="Exception"></exception>
                int M<T>(int goo)
                {
                    if (goo < 0) throw new /*leading trivia*/Exception/*trailing trivia*/();
                    return 0;
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/54245")]
    public void TypingCharacter_Constructor_WithExceptions()
    {
        var code =
            """
            class C
            {
                //$$
                public C(int goo)
                {
                    if (goo < 0) throw new /*leading trivia*/Exception/*trailing trivia*/();
                    throw null;
                    throw null;
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <param name="goo"></param>
                /// <exception cref="Exception"></exception>
                /// <exception cref="System.NullReferenceException"></exception>
                public C(int goo)
                {
                    if (goo < 0) throw new /*leading trivia*/Exception/*trailing trivia*/();
                    throw null;
                    throw null;
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/54245")]
    public void TypingCharacter_Constructor_WithExceptions_Caught()
    {
        // This result is wrong, but we can't do better as long as we only check syntax.
        var code = """

            using System;

            class C
            {
                //$$
                public C(int goo)
                {
                    try
                    {
                        if (goo == 10)
                            throw new Exception();
                        if (goo == 9)
                            throw new ArgumentOutOfRangeException();
                    }
                    catch (ArgumentException)
                    {
                    }

                    throw null;
                    throw null;
                }
            }
            """;

        var expected = """

            using System;

            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <param name="goo"></param>
                /// <exception cref="Exception"></exception>
                /// <exception cref="ArgumentOutOfRangeException"></exception>
                /// <exception cref="NullReferenceException"></exception>
                public C(int goo)
                {
                    try
                    {
                        if (goo == 10)
                            throw new Exception();
                        if (goo == 9)
                            throw new ArgumentOutOfRangeException();
                    }
                    catch (ArgumentException)
                    {
                    }

                    throw null;
                    throw null;
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_Method_WithVerbatimParams()
    {
        var code =
            """
            class C
            {
                //$$
                int M<@int>(int @goo) { return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="int"></typeparam>
                /// <param name="goo"></param>
                /// <returns></returns>
                int M<@int>(int @goo) { return 0; }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_AutoProperty()
    {
        var code =
            """
            class C
            {
                //$$
                int P { get; set; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                int P { get; set; }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_Property()
    {
        var code =
            """
            class C
            {
                //$$
                int P
                {
                    get { return 0; }
                    set { }
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                int P
                {
                    get { return 0; }
                    set { }
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_Indexer()
    {
        var code =
            """
            class C
            {
                //$$
                int this[int index]
                {
                    get { return 0; }
                    set { }
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <param name="index"></param>
                /// <returns></returns>
                int this[int index]
                {
                    get { return 0; }
                    set { }
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_VoidMethod1()
    {
        var code =
            """
            class C
            {
                //$$
                void M<T>(int goo) {  }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="goo"></param>
                void M<T>(int goo) {  }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_VoidMethod_WithVerbatimParams()
    {
        var code =
            """
            class C
            {
                //$$
                void M<@T>(int @int) {  }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="int"></param>
                void M<@T>(int @int) {  }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538699")]
    public void TypingCharacter_VoidMethod2()
    {
        var code =
            """
            class C
            {
                //$$
                void Method() { }
            }
            """;
        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                void Method() { }
            }
            """;
        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotWhenDocCommentExists1()
    {
        var code = """

            ///
            //$$
            class C
            {
            }
            """;

        var expected = """

            ///
            ///$$
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotWhenDocCommentExists2()
    {
        var code = """

            ///

            //$$
            class C
            {
            }
            """;

        var expected = """

            ///

            ///$$
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotWhenDocCommentExists3()
    {
        var code = """

            class B { } ///

            //$$
            class C
            {
            }
            """;

        var expected = """

            class B { } ///

            ///$$
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotWhenDocCommentExists4()
    {
        var code =
            """
            //$$
            /// <summary></summary>
            class C
            {
            }
            """;

        var expected =
            """
            ///$$
            /// <summary></summary>
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotWhenDocCommentExists5()
    {
        var code =
            """
            class C
            {
                //$$
                /// <summary></summary>
                int M<T>(int goo) { return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                ///$$
                /// <summary></summary>
                int M<T>(int goo) { return 0; }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotInsideMethodBody1()
    {
        var code =
            """
            class C
            {
                void M(int goo)
                {
                  //$$
                }
            }
            """;

        var expected =
            """
            class C
            {
                void M(int goo)
                {
                  ///$$
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotInsideMethodBody2()
    {
        var code =
            """
            class C
            {
                /// <summary></summary>
                void M(int goo)
                {
                  //$$
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary></summary>
                void M(int goo)
                {
                  ///$$
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotAfterClassName()
    {
        var code =
            """
            class C//$$
            {
            }
            """;

        var expected =
            """
            class C///$$
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotAfterOpenBrace()
    {
        var code =
            """
            class C
            {//$$
            }
            """;

        var expected =
            """
            class C
            {///$$
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotAfterCtorName()
    {
        var code =
            """
            class C
            {
            C() //$$
            }
            """;

        var expected =
            """
            class C
            {
            C() ///$$
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_NotInsideCtor()
    {
        var code =
            """
            class C
            {
            C()
            {
            //$$
            }
            }
            """;

        var expected =
            """
            class C
            {
            C()
            {
            ///$$
            }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/59081")]
    public void TypingCharacter_NotInTopLevel()
    {
        var code = """

            using System;

            //$$
            Console.WriteLine();

            """;

        var expected = """

            using System;

            ///$$
            Console.WriteLine();

            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/59081")]
    public void TypingCharacter_NotInNamespace()
    {
        var code = """

            using System;

            //$$
            namespace NS { }

            """;

        var expected = """

            using System;

            ///$$
            namespace NS { }

            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertComment_Class1()
    {
        var code =
            """
            ///$$
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/4817")]
    public void PressingEnter_InsertComment_Class1_AutoGenerateXmlDocCommentsOff()
    {
        var code =
            """
            ///$$
            class C
            {
            }
            """;

        var expected =
            """
            ///
            $$
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected, globalOptions: new OptionsCollection(LanguageNames.CSharp)
        {
            { DocumentationCommentOptionsStorage.AutoXmlDocCommentGeneration, false }
        });
    }

    [WpfFact]
    public void PressingEnter_InsertComment_Class2()
    {
        var code =
            """
            ///$$class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertComment_Class3()
    {
        var code =
            """
            ///$$[Goo] class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            [Goo] class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertComment_NotAfterWhitespace()
    {
        var code =
        """
        ///    $$class C
        {
        }
        """;

        var expected =
            """
            ///    
            /// $$class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertComment_Method1()
    {
        var code =
            """
            class C
            {
                ///$$
                int M<T>(int goo) { return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="goo"></param>
                /// <returns></returns>
                int M<T>(int goo) { return 0; }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertComment_Method2()
    {
        var code =
            """
            class C
            {
                ///$$int M<T>(int goo) { return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="goo"></param>
                /// <returns></returns>
                int M<T>(int goo) { return 0; }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_NotInMethodBody1()
    {
        var code =
            """
            class C
            {
            void Goo()
            {
            ///$$
            }
            }
            """;

        var expected =
            """
            class C
            {
            void Goo()
            {
            ///
            $$
            }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537513")]
    public void PressingEnter_NotInterleavedInClassName1()
    {
        var code =
            """
            class///$$ C
            {
            }
            """;

        var expected =
            """
            class///
            $$ C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537513")]
    public void PressingEnter_NotInterleavedInClassName2()
    {
        var code =
            """
            class ///$$C
            {
            }
            """;

        var expected =
            """
            class ///
            $$C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537513")]
    public void PressingEnter_NotInterleavedInClassName3()
    {
        var code =
            """
            class /// $$C
            {
            }
            """;

        var expected =
            """
            class /// 
            $$C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537514")]
    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537532")]
    public void PressingEnter_NotAfterClassName1()
    {
        var code =
            """
            class C ///$$
            {
            }
            """;

        var expected =
            """
            class C ///
            $$
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537552")]
    public void PressingEnter_NotAfterClassName2()
    {
        var code =
            """
            class C /** $$
            {
            }
            """;

        var expected =
            """
            class C /** 
            $$
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537535")]
    public void PressingEnter_NotAfterCtorName()
    {
        var code =
            """
            class C
            {
            C() ///$$
            }
            """;

        var expected =
            """
            class C
            {
            C() ///
            $$
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537511")]
    public void PressingEnter_NotInsideCtor()
    {
        var code =
            """
            class C
            {
            C()
            {
            ///$$
            }
            }
            """;

        var expected =
            """
            class C
            {
            C()
            {
            ///
            $$
            }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/537550")]
    public void PressingEnter_NotBeforeDocComment()
    {
        var code =
            """
                class c1
                {
            $$/// <summary>
                    /// 
                    /// </summary>
                    /// <returns></returns>
                    public async Task goo()
                    {
                        var x = 1;
                    }
                }
            """;

        var expected =
            """
                class c1
                {

            $$/// <summary>
                    /// 
                    /// </summary>
                    /// <returns></returns>
                    public async Task goo()
                    {
                        var x = 1;
                    }
                }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes1()
    {
        var code =
            """
            ///$$
            /// <summary></summary>
            class C
            {
            }
            """;

        var expected =
            """
            ///
            /// $$
            /// <summary></summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes2()
    {
        var code =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// 
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes3()
    {
        var code =
            """
                /// <summary>
                /// $$
                /// </summary>
                class C
                {
                }
            """;

        var expected =
            """
                /// <summary>
                /// 
                /// $$
                /// </summary>
                class C
                {
                }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes4()
    {
        var code =
            """
            /// <summary>$$</summary>
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$</summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes5()
    {
        var code =
            """
                /// <summary>
                /// $$
                /// </summary>
                class C
                {
                }
            """;

        var expected =
            """
                /// <summary>
                /// 
                /// $$
                /// </summary>
                class C
                {
                }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes6()
    {
        var code =
            """
            /// <summary></summary>$$
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary></summary>
            /// $$
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes7()
    {
        var code =
            """
                /// <summary>$$</summary>
                class C
                {
                }
            """;

        var expected =
            """
                /// <summary>
                /// $$</summary>
                class C
                {
                }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538702")]
    public void PressingEnter_InsertSlashes8()
    {
        var code =
            """
            /// <summary>
            /// 
            /// </summary>
            ///$$class C {}
            """;
        var expected =
            """
            /// <summary>
            /// 
            /// </summary>
            ///
            /// $$class C {}
            """;
        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes9()
    {
        var code =
            """
            class C
            {
                ///$$
                /// <summary></summary>
                int M<T>(int goo) { return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                ///
                /// $$
                /// <summary></summary>
                int M<T>(int goo) { return 0; }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes10()
    {
        var code =
            """
            /// <summary>
            /// 
            /// </summary>
            ///$$Go ahead and add some slashes
            """;
        var expected =
            """
            /// <summary>
            /// 
            /// </summary>
            ///
            /// $$Go ahead and add some slashes
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void PressingEnter_InsertSlashes11()
    {
        var code =
            """
            class C
            {
                /// <summary>
                /// 
                /// </summary>
                /// <param name="i">$$</param>
                void Goo(int i)
                {
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// 
                /// </summary>
                /// <param name="i">
                /// $$</param>
                void Goo(int i)
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/4817")]
    public void PressingEnter_InsertSlashes12_AutoGenerateXmlDocCommentsOff()
    {
        var code =
            """
            ///$$
            /// <summary></summary>
            class C
            {
            }
            """;

        var expected =
            """
            ///
            /// $$
            /// <summary></summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected, globalOptions: new OptionsCollection(LanguageNames.CSharp)
        {
            { DocumentationCommentOptionsStorage.AutoXmlDocCommentGeneration, false }
        });
    }

    [WpfFact]
    public void PressingEnter_DoNotInsertSlashes1()
    {
        var code =
            """
            /// <summary></summary>
            /// $$
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary></summary>
            /// 
            $$
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538701")]
    public void PressingEnter_DoNotInsertSlashes2()
    {
        var code =
            """
            ///<summary></summary>

            ///$$
            class C{}
            """;
        var expected =
            """
            ///<summary></summary>

            ///
            $$
            class C{}
            """;
        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/25746")]
    public void PressingEnter_ExtraSlashesAfterExteriorTrivia()
    {
        var code =
            """
            class C
            {
            C()
            {
            //////$$
            }
            }
            """;

        var expected =
            """
            class C
            {
            C()
            {
            //////
            ///$$
            }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/542426")]
    public void PressingEnter_PreserveParams()
    {
        var code =
            """
            /// <summary>
            /// 
            /// </summary>
            /// <param name="args">$$</param>
            static void Main(string[] args)
            { }
            """;
        var expected =
            """
            /// <summary>
            /// 
            /// </summary>
            /// <param name="args">
            /// $$</param>
            static void Main(string[] args)
            { }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2091")]
    public void PressingEnter_InTextBeforeSpace()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                /// hello$$ world
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                /// hello
                /// $$world
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2108")]
    public void PressingEnter_Indentation1()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                ///     hello world$$
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                ///     hello world
                ///     $$
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2108")]
    public void PressingEnter_Indentation2()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                ///     hello $$world
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                ///     hello 
                ///     $$world
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2108")]
    public void PressingEnter_Indentation3()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                ///     hello$$ world
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                ///     hello
                ///     $$world
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2108")]
    public void PressingEnter_Indentation4()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                ///     $$hello world
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                ///     
                /// $$hello world
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2108")]
    public void PressingEnter_Indentation5_UseTabs()
    {
        const string code =
            """
            class C
            {
            	/// <summary>
            	///     hello world$$
            	/// </summary>
            	void M()
            	{
            	}
            }
            """;

        const string expected =
            """
            class C
            {
            	/// <summary>
            	///     hello world
            	///     $$
            	/// </summary>
            	void M()
            	{
            	}
            }
            """;

        VerifyPressingEnter(code, expected, useTabs: true);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/5486")]
    public void PressingEnter_Selection1()
    {
        var code =
            """
            /// <summary>
            /// Hello [|World|]$$!
            /// </summary>
            class C
            {
            }
            """;
        var expected =
            """
            /// <summary>
            /// Hello 
            /// $$!
            /// </summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/5486")]
    public void PressingEnter_Selection2()
    {
        var code =
            """
            /// <summary>
            /// Hello $$[|World|]!
            /// </summary>
            class C
            {
            }
            """;
        var expected =
            """
            /// <summary>
            /// Hello 
            /// $$!
            /// </summary>
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    [WorkItem("https://github.com/dotnet/roslyn/issues/27223")]
    [WorkItem("https://github.com/dotnet/roslyn/issues/49564")]
    public void PressingEnter_XmlDocCommentInStringLiteral()
    {
        var code =
            """
            class C
            {
                C()
                {
                    string s = @"
                        /// <summary>$$</summary>
                        void M() {}"
                }
            }
            """;

        var expected =
            """
            class C
            {
                C()
                {
                    string s = @"
                        /// <summary>
            $$</summary>
                        void M() {}"
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact]
    public void Command_Class()
    {
        var code =
            """
            class C
            {$$
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_Record()
    {
        var code = "record R$$;";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            record R;
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_RecordStruct()
    {
        var code = "record struct R$$;";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            record struct R;
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_RecordWithPositionalParameters()
    {
        var code = "record R$$(string S, int I);";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            record R(string S, int I);
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_ClassParameters()
    {
        var code = "class R$$(string S, int I);";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            class R(string S, int I);
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_RecordStructWithPositionalParameters()
    {
        var code = "record struct R$$(string S, int I);";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            record struct R(string S, int I);
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_StructParameters()
    {
        var code = "struct R$$(string S, int I);";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            /// <param name="S"></param>
            /// <param name="I"></param>
            struct R(string S, int I);
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/4817")]
    public void Command_Class_AutoGenerateXmlDocCommentsOff()
    {
        var code =
            """
            class C
            {$$
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C
            {
            }
            """;

        VerifyInsertCommentCommand(code, expected, globalOptions: new OptionsCollection(LanguageNames.CSharp)
        {
            { DocumentationCommentOptionsStorage.AutoXmlDocCommentGeneration, false }
        });
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538714")]
    public void Command_BeforeClass1()
    {
        var code =
            """
            $$
            class C { }
            """;
        var expected =
            """

            /// <summary>
            /// $$
            /// </summary>
            class C { }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538714")]
    public void Command_BeforeClass2()
    {
        var code =
            """
            class B { }
            $$
            class C { }
            """;
        var expected =
            """
            class B { }

            /// <summary>
            /// $$
            /// </summary>
            class C { }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538714")]
    public void Command_BeforeClass3()
    {
        var code =
            """
            class B
            {
                $$
                class C { }
            }
            """;
        var expected =
            """
            class B
            {
                
                /// <summary>
                /// $$
                /// </summary>
                class C { }
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/527604")]
    public void Command_Class_NotIfMultilineDocCommentExists()
    {
        var code =
            """
            /**
            */
            class C { $$ }
            """;

        var expected =
            """
            /**
            */
            class C { $$ }
            """;
        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_Method()
    {
        var code =
            """
            class C
            {
                int M<T>(int goo) { $$return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                /// <typeparam name="T"></typeparam>
                /// <param name="goo"></param>
                /// <returns></returns>
                int M<T>(int goo) { return 0; }
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_Class_NotIfCommentExists()
    {
        var code =
            """
            /// <summary></summary>
            class C
            {$$
            }
            """;

        var expected =
            """
            /// <summary></summary>
            class C
            {$$
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact]
    public void Command_Method_NotIfCommentExists()
    {
        var code =
            """
            class C
            {
                /// <summary></summary>
                int M<T>(int goo) { $$return 0; }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary></summary>
                int M<T>(int goo) { $$return 0; }
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538482")]
    public void Command_FirstClassOnLine()
    {
        var code = @"$$class C { } class D { }";

        var expected =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C { } class D { }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538482")]
    public void Command_NotOnSecondClassOnLine()
    {
        var code = @"class C { } $$class D { }";

        var expected = @"class C { } $$class D { }";

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538482")]
    public void Command_FirstMethodOnLine()
    {
        var code =
            """
            class C
            {
                protected abstract void $$Goo(); protected abstract void Bar();
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// </summary>
                protected abstract void Goo(); protected abstract void Bar();
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/538482")]
    public void Command_NotOnSecondMethodOnLine()
    {
        var code =
            """
            class C
            {
                protected abstract void Goo(); protected abstract void $$Bar();
            }
            """;

        var expected =
            """
            class C
            {
                protected abstract void Goo(); protected abstract void $$Bar();
            }
            """;

        VerifyInsertCommentCommand(code, expected);
    }

    [WpfFact, WorkItem("http://vstfdevdiv:8080/DevDiv2/DevDiv/_workitems/edit/917904")]
    public void TestUseTab()
    {
        var code =
            """
            using System;

            public class Class1
            {
            	//$$
            	public Class1()
            	{
            	}
            }
            """;

        var expected =
            """
            using System;

            public class Class1
            {
            	/// <summary>
            	/// $$
            	/// </summary>
            	public Class1()
            	{
            	}
            }
            """;

        VerifyTypingCharacter(code, expected, useTabs: true);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineAbove1()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                /// stuff$$
                /// </summary>
                void M()
                {
                }
            }
            """;

        var expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// stuff
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyOpenLineAbove(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineAbove2()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                /// $$stuff
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                /// $$
                /// stuff
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyOpenLineAbove(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineAbove3()
    {
        const string code =
            """
            class C
            {
                /// $$<summary>
                /// stuff
                /// </summary>
                void M()
                {
                }
            }
            """;

        // Note that the caret position specified below does not look correct because
        // it is in virtual space in this case.
        const string expected =
            """
            class C
            {
            $$
                /// <summary>
                /// stuff
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyOpenLineAbove(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineAbove4_Tabs()
    {
        const string code =
            """
            class C
            {
            		  /// <summary>
            	/// $$stuff
            	/// </summary>
            	void M()
            	{
            	}
            }
            """;

        const string expected =
            """
            class C
            {
            		  /// <summary>
            		  /// $$
            	/// stuff
            	/// </summary>
            	void M()
            	{
            	}
            }
            """;

        VerifyOpenLineAbove(code, expected, useTabs: true);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineBelow1()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                /// stuff$$
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                /// stuff
                /// $$
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyOpenLineBelow(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineBelow2()
    {
        const string code =
            """
            class C
            {
                /// <summary>
                /// $$stuff
                /// </summary>
                void M()
                {
                }
            }
            """;

        const string expected =
            """
            class C
            {
                /// <summary>
                /// stuff
                /// $$
                /// </summary>
                void M()
                {
                }
            }
            """;

        VerifyOpenLineBelow(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineBelow3()
    {
        const string code =
            """
            /// <summary>
            /// stuff
            /// $$</summary>

            """;

        const string expected =
            """
            /// <summary>
            /// stuff
            /// </summary>
            /// $$

            """;

        VerifyOpenLineBelow(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/2090")]
    public void TestOpenLineBelow4_Tabs()
    {
        const string code =
            """
            class C
            {
            	/// <summary>
            		  /// $$stuff
            	/// </summary>
            	void M()
            	{
            	}
            }
            """;

        const string expected =
            """
            class C
            {
            	/// <summary>
            		  /// stuff
            		  /// $$
            	/// </summary>
            	void M()
            	{
            	}
            }
            """;

        VerifyOpenLineBelow(code, expected, useTabs: true);
    }

    [WpfFact, WorkItem(468638, @"https://devdiv.visualstudio.com/DevDiv/NET%20Developer%20Experience%20IDE/_workitems/edit/468638")]
    public void VerifyEnterWithTrimNewLineEditorConfigOption()
    {
        const string code =
            """
            /// <summary>
            /// $$
            /// </summary>
            class C { }
            """;

        const string expected =
            """
            /// <summary>
            ///
            /// $$
            /// </summary>
            class C { }
            """;

        VerifyPressingEnter(code, expected, useTabs: true, trimTrailingWhiteSpace: true);
    }

    [WpfFact]
    public void TypingCharacter_Class_WithComment()
    {
        var code =
            """
            //$$ This is my class and it does great things.
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$This is my class and it does great things.
            /// </summary>
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact]
    public void TypingCharacter_Class_WithComment_NoSpace()
    {
        var code =
            """
            //$$This is my class and it does great things.
            class C
            {
            }
            """;

        var expected =
            """
            /// <summary>
            /// $$This is my class and it does great things.
            /// </summary>
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/75838")]
    public void TypingCharacter_ExistingText1()
    {
        var code =
            """
            /// foo$$
            class C
            {
            }
            """;

        var expected =
            """
            /// foo/$$
            class C
            {
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/75838")]
    public void TypingCharacter_ExistingText2()
    {
        var code =
            """
            namespace N
            {
                /// foo$$
                class C
                {
                }
            }
            """;

        var expected =
            """
            namespace N
            {
                /// foo/$$
                class C
                {
                }
            }
            """;

        VerifyTypingCharacter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/75838")]
    public void PressingEnter_ExistingText1()
    {
        var code =
            """
            /// foo$$
            class C
            {
            }
            """;

        var expected =
            """
            /// foo
            /// $$
            class C
            {
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    [WpfFact, WorkItem("https://github.com/dotnet/roslyn/issues/75838")]
    public void PressingEnter_ExistingText2()
    {
        var code =
            """
            namespace N
            {
                /// foo$$
                class C
                {
                }
            }
            """;

        var expected =
            """
            namespace N
            {
                /// foo
                /// $$
                class C
                {
                }
            }
            """;

        VerifyPressingEnter(code, expected);
    }

    protected override char DocumentationCommentCharacter
    {
        get { return '/'; }
    }

    internal override ICommandHandler CreateCommandHandler(EditorTestWorkspace workspace)
    {
        return workspace.ExportProvider.GetCommandHandler<DocumentationCommentCommandHandler>(PredefinedCommandHandlerNames.DocumentationComments, ContentTypeNames.CSharpContentType);
    }

    protected override EditorTestWorkspace CreateTestWorkspace(string code)
        => EditorTestWorkspace.CreateCSharp(code);
}
