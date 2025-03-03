﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Editor.InlineRename;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.CodeAnalysis.Editor.Implementation.InlineRename
{
    internal class InlineRenameAdornmentManager : IDisposable
    {
        private readonly IWpfTextView _textView;
        private readonly IGlobalOptionService _globalOptionService;
        private readonly IWpfThemeService? _themeService;
        private readonly IAsyncQuickInfoBroker _asyncQuickInfoBroker;
        private readonly IAsynchronousOperationListenerProvider _listenerProvider;
        private readonly InlineRenameService _renameService;
        private readonly IEditorFormatMapService _editorFormatMapService;
        private readonly IInlineRenameColorUpdater? _dashboardColorUpdater;

        private readonly IAdornmentLayer _adornmentLayer;

        private static readonly ConditionalWeakTable<InlineRenameSession, object> s_createdViewModels =
            new ConditionalWeakTable<InlineRenameSession, object>();

        public InlineRenameAdornmentManager(
            InlineRenameService renameService,
            IEditorFormatMapService editorFormatMapService,
            IInlineRenameColorUpdater? dashboardColorUpdater,
            IWpfTextView textView,
            IGlobalOptionService globalOptionService,
            IWpfThemeService? themeService,
            IAsyncQuickInfoBroker asyncQuickInfoBroker,
            IAsynchronousOperationListenerProvider listenerProvider)
        {
            _renameService = renameService;
            _editorFormatMapService = editorFormatMapService;
            _dashboardColorUpdater = dashboardColorUpdater;
            _textView = textView;
            _globalOptionService = globalOptionService;
            _themeService = themeService;
            _asyncQuickInfoBroker = asyncQuickInfoBroker;
            _listenerProvider = listenerProvider;
            _adornmentLayer = textView.GetAdornmentLayer(InlineRenameAdornmentProvider.AdornmentLayerName);

            _renameService.ActiveSessionChanged += OnActiveSessionChanged;
            _textView.Closed += OnTextViewClosed;

            UpdateAdornments();
        }

        public void Dispose()
        {
            _renameService.ActiveSessionChanged -= OnActiveSessionChanged;
            _textView.Closed -= OnTextViewClosed;
        }

        private void OnTextViewClosed(object sender, EventArgs e)
            => Dispose();

        private void OnActiveSessionChanged(object sender, EventArgs e)
            => UpdateAdornments();

        private void UpdateAdornments()
        {
            _adornmentLayer.RemoveAllAdornments();

            if (_renameService.ActiveSession != null &&
                ViewIncludesBufferFromWorkspace(_textView, _renameService.ActiveSession.Workspace))
            {
                _dashboardColorUpdater?.UpdateColors();

                var adornment = GetAdornment();

                if (adornment is null)
                {
                    return;
                }

                _themeService?.ApplyThemeToElement(adornment);

                _adornmentLayer.AddAdornment(
                    AdornmentPositioningBehavior.ViewportRelative,
                    null, // Set no visual span because we don't want the editor to automatically remove the adornment if the overlapping span changes
                    tag: null,
                    adornment,
                    (tag, adornment) => ((InlineRenameAdornment)adornment).Dispose());
            }
        }

        private InlineRenameAdornment? GetAdornment()
        {
            if (_renameService.ActiveSession is null)
            {
                return null;
            }

            var useInlineAdornment = _globalOptionService.GetOption(InlineRenameUIOptions.UseInlineAdornment);
            if (useInlineAdornment)
            {
                if (!_textView.HasAggregateFocus)
                {
                    // For the rename flyout, the adornment is dismissed on focus lost. There's
                    // no need to keep an adornment on every textview for show/hide behaviors
                    return null;
                }

                // Get the active selection to make sure the rename text is selected in the same way
                var originalSpan = _renameService.ActiveSession.TriggerSpan;
                var selectionSpan = _textView.Selection.SelectedSpans.First();

                var start = selectionSpan.IsEmpty
                    ? 0
                    : selectionSpan.Start - originalSpan.Start; // The length from the identifier to the start of selection

                var length = selectionSpan.IsEmpty
                    ? originalSpan.Length
                    : selectionSpan.Length;

                var identifierSelection = new TextSpan(start, length);

                var adornment = new RenameFlyout(
                    (RenameFlyoutViewModel)s_createdViewModels.GetValue(_renameService.ActiveSession, session => new RenameFlyoutViewModel(session, identifierSelection, registerOleComponent: true, _globalOptionService)),
                    _textView,
                    _themeService,
                    _asyncQuickInfoBroker,
                    _listenerProvider);

                return adornment;
            }
            else
            {
                var newAdornment = new RenameDashboard(
                    (RenameDashboardViewModel)s_createdViewModels.GetValue(_renameService.ActiveSession, session => new RenameDashboardViewModel(session)),
                    _editorFormatMapService,
                    _textView);

                return newAdornment;
            }
        }

        private static bool ViewIncludesBufferFromWorkspace(IWpfTextView textView, Workspace workspace)
        {
            return textView.BufferGraph.GetTextBuffers(b => GetWorkspace(b.AsTextContainer()) == workspace)
                                       .Any();
        }

        private static Workspace? GetWorkspace(SourceTextContainer textContainer)
        {
            Workspace.TryGetWorkspace(textContainer, out var workspace);
            return workspace;
        }
    }
}
