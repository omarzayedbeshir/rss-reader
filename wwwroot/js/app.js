const state = {
    feeds: [],
    articles: [],
    selectedFeedId: null,
    loading: false,
    currentPage: 1,
    totalPages: 1,
    pageSize: 20,
    token: localStorage.getItem('token') || null,
    email: localStorage.getItem('email') || null,
    userId: localStorage.getItem('userId') || null
};

const elements = {
    authView: document.getElementById('auth-view'),
    appView: document.getElementById('app-view'),
    authForm: document.getElementById('auth-form'),
    authFields: document.getElementById('auth-fields'),
    authEmail: document.getElementById('auth-email'),
    authPassword: document.getElementById('auth-password'),
    authSubmit: document.getElementById('auth-submit'),
    authToggleBtn: document.getElementById('auth-toggle-btn'),
    authError: document.getElementById('auth-error'),
    authMessage: document.getElementById('auth-message'),
    resendSection: document.getElementById('resend-section'),
    resendVerificationBtn: document.getElementById('resend-verification-btn'),
    currentUser: document.getElementById('current-user'),
    signoutBtn: document.getElementById('signout-btn'),
    feedList: document.getElementById('feed-list'),
    articleList: document.getElementById('article-list'),
    loading: document.getElementById('loading'),
    error: document.getElementById('error'),
    empty: document.getElementById('empty'),
    refreshAllBtn: document.getElementById('refresh-all-btn'),
    addFeedBtn: document.getElementById('add-feed-btn'),
    addFeedForm: document.getElementById('add-feed-form'),
    feedForm: document.getElementById('feed-form'),
    feedUrlInput: document.getElementById('feed-url'),
    cancelAddBtn: document.getElementById('cancel-add-btn'),
    feedItemTemplate: document.getElementById('feed-item-template'),
    articleCardTemplate: document.getElementById('article-card-template'),
    pagination: document.getElementById('pagination'),
    prevPageBtn: document.getElementById('prev-page-btn'),
    nextPageBtn: document.getElementById('next-page-btn'),
    pageInfo: document.getElementById('page-info')
};

let authMode = 'signin';

function authHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    if (state.token) {
        headers['Authorization'] = 'Bearer ' + state.token;
    }
    return headers;
}

async function apiFetch(url, options = {}) {
    options.headers = { ...authHeaders(), ...options.headers };
    return fetch(url, options);
}

async function checkAuth() {
    if (!state.token) {
        showAuthView();
        return;
    }

    try {
        const response = await apiFetch('/api/auth/me');
        if (!response.ok) {
            clearAuth();
            showAuthView();
            return;
        }
        showAppView();
    } catch {
        clearAuth();
        showAuthView();
    }
}

function clearAuth() {
    state.token = null;
    state.email = null;
    state.userId = null;
    localStorage.removeItem('token');
    localStorage.removeItem('email');
    localStorage.removeItem('userId');
}

function showAuthView() {
    elements.authView.classList.remove('hidden');
    elements.appView.classList.add('hidden');
    checkVerifiedParam();
}

function showAppView() {
    elements.authView.classList.add('hidden');
    elements.appView.classList.remove('hidden');
    elements.currentUser.textContent = state.email || '';
    fetchFeeds(1);
}

function showAuthFields() {
    elements.authFields.style.display = '';
    elements.authSubmit.style.display = '';
    elements.authToggleBtn.parentElement.style.display = '';
    elements.resendSection.style.display = 'none';
    hideAuthMessage();
}

function showVerificationSent(email) {
    elements.authFields.style.display = 'none';
    elements.authSubmit.style.display = 'none';
    elements.authToggleBtn.parentElement.style.display = 'none';
    elements.resendSection.style.display = '';
    showAuthMessage(t('verificationSent', email));
}

async function signIn(email, password) {
    const response = await fetch('/api/auth/signin', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
    });

    const data = await response.json();

    if (!response.ok) {
        throw new Error(data.error || t('signIn') + ' failed.');
    }

    state.token = data.token;
    state.email = data.user.email;
    state.userId = data.user.id;
    localStorage.setItem('token', data.token);
    localStorage.setItem('email', data.user.email);
    localStorage.setItem('userId', data.user.id);
    showAppView();
}

async function signUp(email, password) {
    const response = await fetch('/api/auth/signup', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
    });

    const data = await response.json();

    if (!response.ok) {
        throw new Error(data.error || t('signUp') + ' failed.');
    }

    showVerificationSent(email);
    authMode = 'signin';
    elements.authSubmit.textContent = t('signIn');
    elements.authToggleBtn.textContent = t('createAccount');
    elements.authPassword.value = '';
}

async function resendVerification() {
    const email = elements.authEmail.value.trim();
    if (!email) {
        showAuthError(t('emailPasswordRequired'));
        return;
    }

    try {
        const response = await fetch('/api/auth/resend-verification', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ email })
        });

        const data = await response.json();

        if (!response.ok) {
            throw new Error(data.error || 'Failed to resend verification.');
        }

        showVerificationSent(email);
    } catch (err) {
        showAuthError(tError(err.message));
    }
}

async function signOut() {
    try {
        await apiFetch('/api/auth/signout', { method: 'POST' });
    } catch {}
    clearAuth();
    state.feeds = [];
    state.articles = [];
    state.selectedFeedId = null;
    showAuthView();
}

function showAuthError(message) {
    elements.authError.textContent = message;
    elements.authError.classList.remove('hidden');
}

function hideAuthError() {
    elements.authError.classList.add('hidden');
}

function showAuthMessage(message) {
    elements.authMessage.textContent = message;
    elements.authMessage.classList.remove('hidden');
}

function hideAuthMessage() {
    elements.authMessage.classList.add('hidden');
}

function checkVerifiedParam() {
    const params = new URLSearchParams(window.location.search);
    if (params.get('verified') === '1') {
        showAuthMessage(t('emailVerified'));
        window.history.replaceState({}, '', '/');
    } else if (params.get('verified') === '0') {
        showAuthError(t('invalidVerification'));
        window.history.replaceState({}, '', '/');
    }
    showAuthFields();
}

async function handleAuthSubmit(e) {
    e.preventDefault();
    hideAuthError();
    hideAuthMessage();

    const email = elements.authEmail.value.trim();
    const password = elements.authPassword.value;

    if (!email || !password) {
        showAuthError(t('emailPasswordRequired'));
        return;
    }

    try {
        if (authMode === 'signin') {
            await signIn(email, password);
        } else {
            await signUp(email, password);
        }
    } catch (err) {
        showAuthError(tError(err.message));
    }
}

function toggleAuthMode() {
    authMode = authMode === 'signin' ? 'signup' : 'signin';
    elements.authSubmit.textContent = authMode === 'signin' ? t('signIn') : t('signUp');
    elements.authToggleBtn.textContent = authMode === 'signin' ? t('createAccount') : t('signInInstead');
    hideAuthError();
    hideAuthMessage();
    showAuthFields();
    elements.authPassword.value = '';
}

async function init() {
    if (state.token) elements.authView.classList.add('hidden');

    document.querySelector('.auth-subtitle').textContent = t('authSubtitle');
    document.getElementById('auth-email').placeholder = t('emailPlaceholder');
    document.getElementById('auth-password').placeholder = t('passwordPlaceholder');
    document.getElementById('auth-submit').textContent = t('signIn');
    document.getElementById('auth-toggle-btn').textContent = t('createAccount');
    document.getElementById('signout-btn').textContent = t('signOut');
    document.getElementById('refresh-all-btn').textContent = t('refreshAll');
    document.getElementById('refresh-all-btn').title = t('refreshAll');
    document.getElementById('add-feed-btn').textContent = t('addFeed');
    document.getElementById('feed-url').placeholder = t('feedUrlPlaceholder');
    document.getElementById('loading').textContent = t('loadingArticles');
    document.getElementById('prev-page-btn').textContent = t('prev');
    document.getElementById('next-page-btn').textContent = t('next');

    elements.authForm.addEventListener('submit', handleAuthSubmit);
    elements.authToggleBtn.addEventListener('click', toggleAuthMode);
    elements.resendVerificationBtn.addEventListener('click', resendVerification);
    elements.signoutBtn.addEventListener('click', signOut);

    elements.refreshAllBtn.addEventListener('click', () => refreshAllFeeds());
    elements.prevPageBtn.addEventListener('click', () => fetchFeeds(state.currentPage - 1));
    elements.nextPageBtn.addEventListener('click', () => fetchFeeds(state.currentPage + 1));

    elements.addFeedBtn.addEventListener('click', () => {
        elements.addFeedForm.classList.remove('hidden');
        elements.feedUrlInput.focus();
    });

    elements.cancelAddBtn.addEventListener('click', () => {
        elements.addFeedForm.classList.add('hidden');
        elements.feedUrlInput.value = '';
    });

    elements.feedForm.addEventListener('submit', async (e) => {
        e.preventDefault();
        const url = elements.feedUrlInput.value.trim();
        if (!url) return;
        await addFeed(url);
        elements.addFeedForm.classList.add('hidden');
        elements.feedUrlInput.value = '';
    });

    document.querySelectorAll('.lang-toggle-btn').forEach(function (btn) {
        btn.textContent = t('langLabel');
        btn.addEventListener('click', toggleLang);
    });

    await checkAuth();
}

async function fetchFeeds(page = state.currentPage) {
    showLoading();
    hideError();

    try {
        let url = '/api/feeds?page=' + page + '&pageSize=' + state.pageSize;
        if (state.selectedFeedId) {
            url += '&feedId=' + state.selectedFeedId;
        }

        const response = await apiFetch(url);
        if (!response.ok) throw new Error('Server error: ' + response.status);

        const data = await response.json();
        state.feeds = data.feeds || [];
        state.articles = data.articles || [];
        state.currentPage = data.page;
        state.totalPages = data.totalPages;

        renderFeeds();
        renderArticles();
        renderPagination();
    } catch (err) {
        showError(err.message);
    } finally {
        elements.loading.classList.add('hidden');
    }
}

async function addFeed(url) {
    showLoading();
    hideError();

    try {
        const response = await apiFetch('/api/feeds', {
            method: 'POST',
            body: JSON.stringify({ url })
        });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || t('subscribe') + ' failed');
        }

        await fetchFeeds(1);
    } catch (err) {
        showError(err.message);
    }
}

async function removeFeed(id) {
    if (!confirm(t('removeFeedConfirm'))) return;

    showLoading();
    hideError();

    try {
        const response = await apiFetch('/api/feeds/' + id, { method: 'DELETE' });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || 'Failed to remove feed');
        }

        if (state.selectedFeedId === id) {
            state.selectedFeedId = null;
        }

        await fetchFeeds(1);
    } catch (err) {
        showError(err.message);
    }
}

async function refreshFeed(id) {
    showLoading();
    hideError();

    try {
        await apiFetch('/api/feeds/' + id + '/refresh', { method: 'POST' });
        await fetchFeeds(1);
    } catch (err) {
        showError(err.message);
    }
}

async function refreshAllFeeds() {
    if (state.feeds.length === 0) return;

    showLoading();
    hideError();

    try {
        await Promise.all(state.feeds.map(feed =>
            apiFetch('/api/feeds/' + feed.id + '/refresh', { method: 'POST' })
        ));
        await fetchFeeds(1);
    } catch (err) {
        showError(err.message);
    }
}

function renderFeeds() {
    elements.feedList.innerHTML = '';

    state.feeds.forEach(feed => {
        const template = elements.feedItemTemplate.content.cloneNode(true);
        const li = template.querySelector('.feed-item');
        const titleEl = template.querySelector('.feed-item-title');
        const countEl = template.querySelector('.feed-item-count');
        const refreshBtn = template.querySelector('.feed-item-refresh');
        const removeBtn = template.querySelector('.feed-item-remove');

        titleEl.textContent = feed.title || feed.feedUrl;
        countEl.textContent = feed.articles ? feed.articles.length : 0;

        if (isRtl(feed.title)) {
            li.setAttribute('dir', 'rtl');
        }

        if (state.selectedFeedId === feed.id) {
            li.classList.add('active');
        }

        refreshBtn.title = t('refreshFeedTitle');
        removeBtn.title = t('removeFeedTitle');

        li.addEventListener('click', (e) => {
            if (e.target === refreshBtn || e.target === removeBtn) return;
            state.selectedFeedId = state.selectedFeedId === feed.id ? null : feed.id;
            fetchFeeds(1);
        });

        refreshBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            refreshFeed(feed.id);
        });

        removeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            removeFeed(feed.id);
        });

        elements.feedList.appendChild(template);
    });
}

function renderArticles() {
    elements.articleList.innerHTML = '';

    const articles = state.articles;

    if (articles.length === 0) {
        elements.empty.innerHTML = '<p>' + t('noFeeds') + '</p><p>' + t('clickAddFeed') + '</p>';
        elements.empty.classList.remove('hidden');
        return;
    }

    elements.empty.classList.add('hidden');

    articles.forEach(article => {
        const feed = state.feeds.find(f => f.id === article.feedId);
        const feedTitle = feed ? feed.title : t('unknown');

        const template = elements.articleCardTemplate.content.cloneNode(true);
        const articleEl = template.querySelector('.article-card');
        const badgeEl = template.querySelector('.article-feed-badge');
        const dateEl = template.querySelector('.article-date');
        const titleLinkEl = template.querySelector('.article-title-link');
        const summaryEl = template.querySelector('.article-summary');

        badgeEl.textContent = feedTitle;
        dateEl.textContent = formatDate(article.published);
        titleLinkEl.textContent = article.title;
        titleLinkEl.href = article.url;

        if (isRtl(article.title) || isRtl(article.summary)) {
            articleEl.setAttribute('dir', 'rtl');
            summaryEl.setAttribute('dir', 'rtl');
        }

        summaryEl.innerHTML = article.summary;
        summaryEl.querySelectorAll('a').forEach(a => {
            a.setAttribute('target', '_blank');
            a.setAttribute('rel', 'noopener noreferrer');
        });

        elements.articleList.appendChild(template);
    });
}

function renderPagination() {
    if (state.totalPages <= 1) {
        elements.pagination.classList.add('hidden');
        return;
    }

    elements.pagination.classList.remove('hidden');
    elements.pageInfo.textContent = t('pageOf', state.currentPage, state.totalPages);
    elements.prevPageBtn.disabled = state.currentPage <= 1;
    elements.nextPageBtn.disabled = state.currentPage >= state.totalPages;
}

function isRtl(text) {
    if (!text) return false;
    return /[\u0591-\u08FF\uFB1D-\uFDFD\uFE70-\uFEFC]/.test(text);
}

function formatDate(dateString) {
    if (!dateString) return '';
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);

    if (diffMins < 1) return t('justNow');
    if (diffMins < 60) return t('minutesAgo', diffMins);
    if (diffHours < 24) return t('hoursAgo', diffHours);

    return date.toLocaleDateString(lang === 'ar' ? 'ar' : 'en-US', {
        month: 'short',
        day: 'numeric',
        year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined,
        hour: '2-digit',
        minute: '2-digit'
    });
}

function showLoading() {
    elements.loading.classList.remove('hidden');
    elements.error.classList.add('hidden');
    elements.empty.classList.add('hidden');
    elements.articleList.innerHTML = '';
}

function showError(message) {
    elements.loading.classList.add('hidden');
    elements.error.textContent = message;
    elements.error.classList.remove('hidden');
}

function hideError() {
    elements.error.classList.add('hidden');
}

init();
