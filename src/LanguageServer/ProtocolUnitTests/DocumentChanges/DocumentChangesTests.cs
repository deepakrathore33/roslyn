﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.LanguageServer.Handler.DocumentChanges;
using Roslyn.LanguageServer.Protocol;
using Roslyn.Test.Utilities;
using Xunit;
using Xunit.Abstractions;
using LSP = Roslyn.LanguageServer.Protocol;

namespace Microsoft.CodeAnalysis.LanguageServer.UnitTests.DocumentChanges;

public sealed partial class DocumentChangesTests : AbstractLanguageServerProtocolTests
{
    public DocumentChangesTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
    {
    }

    [Theory, CombinatorialData]
    public async Task DocumentChanges_EndToEnd(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, documentText) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            Assert.Empty(testLspServer.GetTrackedTexts());

            await DidOpen(testLspServer, locationTyped.DocumentUri);

            Assert.Single(testLspServer.GetTrackedTexts());

            var document = testLspServer.GetTrackedTexts().Single();
            Assert.Equal(documentText, document.ToString());

            await DidChange(testLspServer, locationTyped.DocumentUri, (4, 8, "// hi there"));

            document = testLspServer.GetTrackedTexts().Single();
            Assert.Equal("""
                class A
                {
                    void M()
                    {
                        // hi there
                    }
                }
                """, document.ToString());

            await DidClose(testLspServer, locationTyped.DocumentUri);

            Assert.Empty(testLspServer.GetTrackedTexts());
        }
    }

    [Theory, CombinatorialData]
    public async Task DidOpen_DocumentIsTracked(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, documentText) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            var document = testLspServer.GetTrackedTexts().FirstOrDefault();

            AssertEx.NotNull(document);
            Assert.Equal(documentText, document.ToString());
        }
    }

    [Theory, CombinatorialData]
    public async Task MultipleDidOpen_Errors(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, documentText) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await Assert.ThrowsAnyAsync<StreamJsonRpc.RemoteRpcException>(() => DidOpen(testLspServer, locationTyped.DocumentUri));
            await testLspServer.AssertServerShuttingDownAsync();
        }
    }

    [Theory, CombinatorialData]
    public async Task DidCloseWithoutDidOpen_Errors(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, documentText) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await Assert.ThrowsAnyAsync<StreamJsonRpc.RemoteRpcException>(() => DidClose(testLspServer, locationTyped.DocumentUri));
            await testLspServer.AssertServerShuttingDownAsync();
        }
    }

    [Theory, CombinatorialData]
    public async Task DidChangeWithoutDidOpen_Errors(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, documentText) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await Assert.ThrowsAnyAsync<StreamJsonRpc.RemoteRpcException>(() => DidChange(testLspServer, locationTyped.DocumentUri, (0, 0, "goo")));
            await testLspServer.AssertServerShuttingDownAsync();
        }
    }

    [Theory, CombinatorialData]
    public async Task DidClose_StopsTrackingDocument(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidClose(testLspServer, locationTyped.DocumentUri);

            Assert.Empty(testLspServer.GetTrackedTexts());
        }
    }

    [Theory, CombinatorialData]
    public async Task DidChange_AppliesChanges(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidChange(testLspServer, locationTyped.DocumentUri, (4, 8, "// hi there"));

            var document = testLspServer.GetTrackedTexts().FirstOrDefault();

            AssertEx.NotNull(document);
            Assert.Equal("""
                class A
                {
                    void M()
                    {
                        // hi there
                    }
                }
                """, document.ToString());
        }
    }

    [Theory, CombinatorialData]
    public async Task DidChange_DoesntUpdateWorkspace(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, documentText) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidChange(testLspServer, locationTyped.DocumentUri, (4, 8, "// hi there"));

            var documentTextFromWorkspace = (await testLspServer.GetDocumentTextAsync(locationTyped.DocumentUri)).ToString();

            Assert.NotNull(documentTextFromWorkspace);
            Assert.Equal(documentText, documentTextFromWorkspace);

            // Just to ensure this test breaks if didChange stops working for some reason
            Assert.NotEqual("""
                class A
                {
                    void M()
                    {
                        // hi there
                    }
                }
                """, documentTextFromWorkspace);
        }
    }

    [Theory, CombinatorialData]
    public async Task DidChange_MultipleChanges_ForwardOrder(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidChange(testLspServer, locationTyped.DocumentUri, (4, 8, "// hi there"), (5, 0, "        // this builds on that\r\n"));

            var document = testLspServer.GetTrackedTexts().FirstOrDefault();

            AssertEx.NotNull(document);
            Assert.Equal("""
            class A
            {
                void M()
                {
                    // hi there
                    // this builds on that
                }
            }
            """, document.ToString());
        }
    }

    [Theory, CombinatorialData]
    public async Task DidChange_MultipleChanges_Overlapping(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidChange(testLspServer, locationTyped.DocumentUri, (4, 8, "// there"), (4, 11, "hi "));

            var document = testLspServer.GetTrackedTexts().FirstOrDefault();

            AssertEx.NotNull(document);
            Assert.Equal("""
            class A
            {
                void M()
                {
                    // hi there
                }
            }
            """, document.ToString());
        }
    }

    [Theory, CombinatorialData]
    public async Task DidChange_MultipleChanges_ReverseOrder(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidChange(testLspServer, locationTyped.DocumentUri, (5, 0, "        // this builds on that\r\n"), (4, 8, "// hi there"));

            var document = testLspServer.GetTrackedTexts().FirstOrDefault();

            AssertEx.NotNull(document);
            Assert.Equal("""
            class A
            {
                void M()
                {
                    // hi there
                    // this builds on that
                }
            }
            """, document.ToString());
        }
    }

    private LSP.TextDocumentContentChangeEvent CreateTextDocumentContentChangeEvent(int startLine, int startCol, int endLine, int endCol, string newText)
    {
        return new LSP.TextDocumentContentChangeEvent()
        {
            Range = new LSP.Range()
            {
                Start = new LSP.Position(startLine, startCol),
                End = new LSP.Position(endLine, endCol)
            },
            Text = newText
        };
    }

    [Fact]
    public void DidChange_AreChangesInReverseOrder_True()
    {
        LSP.TextDocumentContentChangeEvent change1 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 7, endLine: 0, endCol: 9, newText: "test3");
        LSP.TextDocumentContentChangeEvent change2 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 5, endLine: 0, endCol: 7, newText: "test2");
        LSP.TextDocumentContentChangeEvent change3 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 1, endLine: 0, endCol: 3, newText: "test1");

        Assert.True(DidChangeHandler.AreChangesInReverseOrder([change1, change2, change3]));
    }

    [Fact]
    public void DidChange_AreChangesInReverseOrder_InForwardOrder()
    {
        LSP.TextDocumentContentChangeEvent change1 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 1, endLine: 0, endCol: 3, newText: "test1");
        LSP.TextDocumentContentChangeEvent change2 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 5, endLine: 0, endCol: 7, newText: "test2");
        LSP.TextDocumentContentChangeEvent change3 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 7, endLine: 0, endCol: 9, newText: "test3");

        Assert.False(DidChangeHandler.AreChangesInReverseOrder([change1, change2, change3]));
    }

    [Fact]
    public void DidChange_AreChangesInReverseOrder_Overlapping()
    {
        LSP.TextDocumentContentChangeEvent change1 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 1, endLine: 0, endCol: 3, newText: "test1");
        LSP.TextDocumentContentChangeEvent change2 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 2, endLine: 0, endCol: 4, newText: "test2");
        LSP.TextDocumentContentChangeEvent change3 = CreateTextDocumentContentChangeEvent(startLine: 0, startCol: 3, endLine: 0, endCol: 5, newText: "test3");

        Assert.False(DidChangeHandler.AreChangesInReverseOrder([change1, change2, change3]));
    }

    [Theory, CombinatorialData]
    public async Task DidChange_MultipleRequests(bool mutatingLspWorkspace)
    {
        var (testLspServer, locationTyped, _) = await GetTestLspServerAndLocationAsync("""
            class A
            {
                void M()
                {
                    {|type:|}
                }
            }
            """, mutatingLspWorkspace);

        await using (testLspServer)
        {
            await DidOpen(testLspServer, locationTyped.DocumentUri);

            await DidChange(testLspServer, locationTyped.DocumentUri, (4, 8, "// hi there"));
            await DidChange(testLspServer, locationTyped.DocumentUri, (5, 0, "        // this builds on that\r\n"));

            var document = testLspServer.GetTrackedTexts().FirstOrDefault();

            AssertEx.NotNull(document);
            Assert.Equal("""
            class A
            {
                void M()
                {
                    // hi there
                    // this builds on that
                }
            }
            """, document.ToString());
        }
    }

    private async Task<(TestLspServer, LSP.Location, string)> GetTestLspServerAndLocationAsync(string source, bool mutatingLspWorkspace)
    {
        var testLspServer = await CreateTestLspServerAsync(source, mutatingLspWorkspace, CapabilitiesWithVSExtensions);
        var locationTyped = testLspServer.GetLocations("type").Single();
        var documentText = await testLspServer.GetDocumentTextAsync(locationTyped.DocumentUri);

        return (testLspServer, locationTyped, documentText.ToString());
    }

    private static Task DidOpen(TestLspServer testLspServer, DocumentUri uri) => testLspServer.OpenDocumentAsync(uri);

    private static async Task DidChange(TestLspServer testLspServer, DocumentUri uri, params (int line, int column, string text)[] changes)
        => await testLspServer.InsertTextAsync(uri, changes);

    private static async Task DidClose(TestLspServer testLspServer, DocumentUri uri) => await testLspServer.CloseDocumentAsync(uri);
}
