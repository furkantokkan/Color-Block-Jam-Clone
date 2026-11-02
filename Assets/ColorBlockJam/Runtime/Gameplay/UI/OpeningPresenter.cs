using System;
using System.Collections.Generic;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// Shows the starting boosters of an opening (B-16, reference 11): the loader passes the committed plan on Home, and
    /// the presenter lists one icon per booster the plan includes, in booster catalog order, so any number of starting
    /// boosters fits the layer. The dimmed layer shows while the session's opening intro dims the screen; the Rocket
    /// flies after it fades. The fade is a USS transition; nothing here runs on its own.
    /// </summary>
    /// <remarks>
    /// Owner: the gameplay HUD. Its one subscription goes into a CompositeDisposable that <see cref="Unbind"/> clears;
    /// <see cref="Dispose"/> is the last stop point. The session ends the intro at Teardown, which hides the layer. The
    /// icon list is filled on Home only (SetPlan), never in a Playing tick.
    /// </remarks>
    public sealed class OpeningPresenter : IDisposable
    {
        private readonly LevelSession m_session;
        private readonly BoosterCatalog m_catalog;
        private readonly List<Sprite> m_icons = new List<Sprite>();
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Observer<bool> m_onDimmedChanged;
        private IGameplayHudView m_view;
        private bool m_disposed;

        public OpeningPresenter(LevelSession session, BoosterCatalog catalog)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_onDimmedChanged = OnDimmedChanged;
        }

        public bool IsBound => m_view != null;

        public void Bind(IGameplayHudView view)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(OpeningPresenter));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Opening presenter: already bound; unbind first.");
            }

            m_view = view ?? throw new ArgumentNullException(nameof(view));
            m_icons.Clear();
            m_view.SetOpeningIcons(m_icons);
            m_subscriptions.Add(m_session.IsOpeningDimmed.Subscribe(m_onDimmedChanged));
        }

        /// <summary>Chooses the icons of the opening Play is about to start; an empty plan shows none.</summary>
        public void SetPlan(in OpeningPlan plan)
        {
            if (m_view == null)
            {
                return;
            }

            m_icons.Clear();
            for (int index = 0; index < m_catalog.Count; index++)
            {
                BoosterDefinition booster = m_catalog.GetAt(index);
                if (!(booster is null) && booster.Icon != null && plan.Includes(booster.Id))
                {
                    m_icons.Add(booster.Icon);
                }
            }

            m_view.SetOpeningIcons(m_icons);
        }

        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            m_view = null;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            m_disposed = true;
        }

        private void OnDimmedChanged(bool dimmed)
        {
            m_view?.SetOpening(dimmed);
        }
    }
}
