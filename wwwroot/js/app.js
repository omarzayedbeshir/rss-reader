const state = {
    feeds: [],
    articles: [],
    digest: null,
    selectedFeedId: null,
    showingBookmarks: false,
    loading: false,
    currentPage: 1,
    totalPages: 1,
    pageSize: 20,
    token: localStorage.getItem('token') || null,
    email: localStorage.getItem('email') || null,
    userId: localStorage.getItem('userId') || null,
    anonymousId: localStorage.getItem('anonymousId') || null,
    autoRefreshTimerId: null,
    showingPosts: false,
    posts: [],
    editingPostId: null,
    handle: localStorage.getItem('handle') || null
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
    summarizeBanner: document.getElementById('summarize-banner'),
    summarizeBannerText: document.querySelector('.summarize-banner-text'),
    summarizeTodayBtn: document.getElementById('summarize-today-btn'),
    dailyDigest: document.getElementById('daily-digest'),
    digestContent: document.querySelector('.digest-content'),
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
    pageInfo: document.getElementById('page-info'),
    postsView: document.getElementById('posts-view'),
    postList: document.getElementById('post-list'),
    postEditor: document.getElementById('post-editor'),
    postForm: document.getElementById('post-form'),
    postTitleInput: document.getElementById('post-title'),
    postContentInput: document.getElementById('post-content'),
    newPostBtn: document.getElementById('new-post-btn'),
    newPostHeaderBtn: document.getElementById('new-post-header-btn'),
    cancelPostBtn: document.getElementById('cancel-post-btn'),
    postEmpty: document.getElementById('post-empty'),
    postItemTemplate: document.getElementById('post-item-template'),
    feedUrlSection: document.getElementById('feed-url-section'),
    feedUrlDisplay: document.getElementById('feed-url-display'),
    copyFeedUrlBtn: document.getElementById('copy-feed-url-btn'),
    feedUrlLabel: document.querySelector('.feed-url-label')
};

let authMode = 'signin';

function authHeaders() {
    const headers = { 'Content-Type': 'application/json' };
    if (state.token) {
        headers['Authorization'] = 'Bearer ' + state.token;
    } else if (state.anonymousId) {
        headers['X-User-Id'] = state.anonymousId;
    }
    return headers;
}

async function apiFetch(url, options = {}) {
    options.headers = { ...authHeaders(), ...options.headers };
    const response = await fetch(url, options);
    if (!response.ok) {
        const text = await response.text();
        var message;
        try { message = JSON.parse(text).error || 'Request failed (' + response.status + ')'; }
        catch { message = 'Request failed (' + response.status + ')'; }
        throw new Error(message);
    }
    return response;
}

async function checkAuth() {
    if (state.token) {
        try {
            const response = await apiFetch('/api/auth/me');
            if (response.ok) {
                const data = await response.json();
                state.handle = data.handle || null;
                if (state.handle) localStorage.setItem('handle', state.handle);
                showAppView();
                return;
            }
        } catch {}
        clearAuth();
        if (state.anonymousId) { showAppView(); return; }
        showAuthView();
        return;
    }

    if (state.anonymousId) {
        showAppView();
        return;
    }

    showAuthView();
}

function clearAuth() {
    state.token = null;
    state.email = null;
    state.userId = null;
    state.handle = null;
    localStorage.removeItem('token');
    localStorage.removeItem('email');
    localStorage.removeItem('userId');
    localStorage.removeItem('handle');
}

function showAuthView() {
    elements.authView.classList.remove('hidden');
    elements.appView.classList.add('hidden');
    elements.signoutBtn.textContent = t('signOut');
    elements.signoutBtn.onclick = signOut;
    if (state.autoRefreshTimerId) {
        clearInterval(state.autoRefreshTimerId);
        state.autoRefreshTimerId = null;
    }
    checkVerifiedParam();
}

function showAuthScreen() {
    clearAuth();
    state.feeds = [];
    state.articles = [];
    state.selectedFeedId = null;
    state.digest = null;
    state.showingPosts = false;
    state.posts = [];
    showAuthView();
}

function continueAsGuest() {
    if (!state.anonymousId) {
        state.anonymousId = crypto.randomUUID();
        localStorage.setItem('anonymousId', state.anonymousId);
    }
    state.token = null;
    state.email = null;
    state.userId = null;
    state.handle = null;
    state.showingPosts = false;
    state.posts = [];
    localStorage.removeItem('token');
    localStorage.removeItem('email');
    localStorage.removeItem('userId');
    localStorage.removeItem('handle');
    showAppView();
}

function showAppView() {
    elements.authView.classList.add('hidden');
    elements.appView.classList.remove('hidden');
    elements.currentUser.textContent = state.token ? (state.email || '') : '';
    if (!state.token) {
        elements.signoutBtn.textContent = t('signIn');
        elements.signoutBtn.onclick = showAuthScreen;
        elements.newPostHeaderBtn.classList.add('hidden');
    } else {
        elements.signoutBtn.textContent = t('signOut');
        elements.signoutBtn.onclick = signOut;
        elements.newPostHeaderBtn.classList.remove('hidden');
    }
    renderFeedUrl();
    if (state.showingPosts) {
        loadPosts();
    } else {
        fetchFeeds(1);
    }
    if (!state.autoRefreshTimerId) {
        state.autoRefreshTimerId = setInterval(refreshAllFeeds, 600000);
    }
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
    state.handle = data.user.handle || null;
    localStorage.setItem('token', data.token);
    localStorage.setItem('email', data.user.email);
    localStorage.setItem('userId', data.user.id);
    if (state.handle) localStorage.setItem('handle', state.handle);
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
    state.digest = null;
    state.showingPosts = false;
    state.posts = [];
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
        elements.authEmail.setAttribute('aria-invalid', 'true');
        elements.authPassword.setAttribute('aria-invalid', 'true');
        showAuthError(t('emailPasswordRequired'));
        return;
    }

    elements.authEmail.removeAttribute('aria-invalid');
    elements.authPassword.removeAttribute('aria-invalid');

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
    var guestBtn = document.getElementById('guest-btn');
    if (guestBtn) guestBtn.textContent = t('guestContinue');
    document.getElementById('signout-btn').textContent = t('signOut');
    document.getElementById('refresh-all-btn').textContent = t('refreshAll');
    document.getElementById('refresh-all-btn').title = t('refreshAll');
    document.getElementById('add-feed-btn').textContent = t('addFeed');
    document.getElementById('feed-url').placeholder = t('feedUrlPlaceholder');
    document.getElementById('loading').textContent = t('loadingArticles');
    document.getElementById('prev-page-btn').textContent = t('prev');
    document.getElementById('next-page-btn').textContent = t('next');

    var postsTitle = elements.postsView.querySelector('.posts-title');
    if (postsTitle) postsTitle.textContent = t('postsView');
    if (elements.postTitleInput) elements.postTitleInput.placeholder = t('postTitle');
    if (elements.postContentInput) elements.postContentInput.placeholder = t('postContent');
    if (elements.newPostBtn) elements.newPostBtn.textContent = t('newPost');
    if (elements.newPostHeaderBtn) elements.newPostHeaderBtn.textContent = t('newPost');
    if (elements.cancelPostBtn) elements.cancelPostBtn.textContent = t('cancel');

    var submitBtn = elements.postForm ? elements.postForm.querySelector('.btn-primary') : null;
    if (submitBtn) submitBtn.textContent = t('save');

    elements.authForm.addEventListener('submit', handleAuthSubmit);
    elements.authToggleBtn.addEventListener('click', toggleAuthMode);
    elements.resendVerificationBtn.addEventListener('click', resendVerification);
    elements.signoutBtn.addEventListener('click', signOut);

    var guestBtn = document.getElementById('guest-btn');
    if (guestBtn) guestBtn.addEventListener('click', continueAsGuest);

    elements.refreshAllBtn.addEventListener('click', () => refreshAllFeeds());
    elements.summarizeTodayBtn.addEventListener('click', () => summarizeToday());
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

    if (elements.postForm) {
        elements.postForm.addEventListener('submit', handlePostSubmit);
    }
    if (elements.newPostBtn) {
        elements.newPostBtn.addEventListener('click', function () {
            showPostEditor(null);
        });
    }
    if (elements.newPostHeaderBtn) {
        elements.newPostHeaderBtn.addEventListener('click', function () {
            showPostEditor(null);
        });
    }
    if (elements.cancelPostBtn) {
        elements.cancelPostBtn.addEventListener('click', hidePostEditor);
    }
    if (elements.copyFeedUrlBtn) {
        elements.copyFeedUrlBtn.addEventListener('click', function () {
            elements.feedUrlDisplay.select();
            document.execCommand('copy');
            elements.copyFeedUrlBtn.textContent = t('urlCopied');
            setTimeout(function () {
                elements.copyFeedUrlBtn.textContent = t('copyUrl');
            }, 2000);
        });
    }

    document.querySelectorAll('.lang-toggle-btn').forEach(function (btn) {
        btn.textContent = t('langLabel');
        btn.addEventListener('click', toggleLang);
    });

    await checkAuth();
}

async function fetchFeeds(page = state.currentPage) {
    if (state.showingPosts) return loadPosts();

    showLoading();
    hideError();

    try {
        let url = '/api/feeds?page=' + page + '&pageSize=' + state.pageSize;
        if (state.showingBookmarks) url += '&bookmarked=true';
        else if (state.selectedFeedId) url += '&feedId=' + state.selectedFeedId;

        const response = await apiFetch(url);
        if (!response.ok) throw new Error('Server error: ' + response.status);

        const data = await response.json();
        state.feeds = data.feeds || [];
        state.articles = data.articles || [];
        state.digest = data.digest || null;
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

async function summarizeToday() {
    if (!state.token) {
        showError('Sign in to use AI features.');
        return;
    }

    elements.summarizeTodayBtn.disabled = true;
    elements.summarizeTodayBtn.textContent = 'Summarizing...';

    try {
        const response = await apiFetch('/api/articles/summarize-today', { method: 'POST' });
        const data = await response.json();
        if (data.digest) {
            state.digest = data.digest;
            renderDigest();
            elements.summarizeBanner.classList.add('hidden');
        }
    } catch (err) {
        showError(err.message);
    } finally {
        elements.summarizeTodayBtn.disabled = false;
        elements.summarizeTodayBtn.textContent = 'Summarize Today';
    }
}

function renderDigest() {
    if (state.digest) {
        elements.digestContent.innerHTML = state.digest
            .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
            .replace(/\n/g, '<br>');
        elements.dailyDigest.classList.remove('hidden');
        elements.summarizeBanner.classList.add('hidden');
    } else {
        elements.dailyDigest.classList.add('hidden');
        updateSummarizeBanner();
    }
}

function updateSummarizeBanner() {
    const todayStart = new Date();
    todayStart.setHours(0, 0, 0, 0);

    const hasTodayArticles = state.articles.some(a => {
        return new Date(a.published) >= todayStart;
    });

    if (!state.digest && hasTodayArticles) {
        const count = state.articles.filter(a => new Date(a.published) >= todayStart).length;
        elements.summarizeBannerText.textContent = count + ' article' + (count !== 1 ? 's' : '') + ' from today. ';
        if (!state.token) {
            elements.summarizeBannerText.textContent += 'Sign in to summarize.';
            elements.summarizeTodayBtn.textContent = 'Sign In';
            elements.summarizeTodayBtn.onclick = showAuthScreen;
        } else {
            elements.summarizeTodayBtn.textContent = 'Summarize Today';
            elements.summarizeTodayBtn.onclick = summarizeToday;
        }
        elements.summarizeBanner.classList.remove('hidden');
    } else {
        elements.summarizeBanner.classList.add('hidden');
    }
}

function toggleFeedSelection(feed) {
    state.selectedFeedId = state.selectedFeedId === feed.id ? null : feed.id;
    fetchFeeds(1);
}

async function toggleBookmark(articleId) {
    const response = await apiFetch('/api/articles/' + articleId + '/toggle-bookmark', { method: 'POST' });
    const data = await response.json();
    return data.bookmarked;
}

function renderFeeds() {
    elements.feedList.innerHTML = '';

    if (state.token) {
        var postsLi = document.createElement('li');
        postsLi.className = 'feed-item feed-item-posts';
        postsLi.setAttribute('role', 'button');
        postsLi.setAttribute('tabindex', '0');
        var postsTitle = document.createElement('span');
        postsTitle.className = 'feed-item-title';
        postsTitle.textContent = t('myPosts');
        postsLi.appendChild(postsTitle);
        if (state.showingPosts) postsLi.classList.add('active');
        postsLi.addEventListener('click', function () {
            state.showingPosts = !state.showingPosts;
            if (state.showingPosts) {
                state.selectedFeedId = null;
                state.showingBookmarks = false;
                loadPosts();
            } else {
                fetchFeeds(1);
            }
        });
        postsLi.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                state.showingPosts = !state.showingPosts;
                if (state.showingPosts) {
                    state.selectedFeedId = null;
                    state.showingBookmarks = false;
                    loadPosts();
                } else {
                    fetchFeeds(1);
                }
            }
        });
        elements.feedList.appendChild(postsLi);
    }

    var bmLi = document.createElement('li');
    bmLi.className = 'feed-item feed-item-bookmarks';
    bmLi.setAttribute('role', 'button');
    bmLi.setAttribute('tabindex', '0');
    var bmTitle = document.createElement('span');
    bmTitle.className = 'feed-item-title';
    bmTitle.textContent = 'Bookmarks \u2605';
    bmLi.appendChild(bmTitle);
    if (state.showingBookmarks) bmLi.classList.add('active');
    bmLi.addEventListener('click', function () {
        state.showingBookmarks = !state.showingBookmarks;
        state.selectedFeedId = null;
        state.showingPosts = false;
        fetchFeeds(1);
    });
    bmLi.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            state.showingBookmarks = !state.showingBookmarks;
            state.selectedFeedId = null;
            state.showingPosts = false;
            fetchFeeds(1);
        }
    });
    elements.feedList.appendChild(bmLi);

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
            li.setAttribute('aria-selected', 'true');
        }

        li.setAttribute('role', 'button');
        li.setAttribute('tabindex', '0');
        li.setAttribute('aria-pressed', state.selectedFeedId === feed.id ? 'true' : 'false');

        refreshBtn.title = t('refreshFeedTitle');
        removeBtn.title = t('removeFeedTitle');

        li.addEventListener('click', (e) => {
            if (e.target === refreshBtn || e.target === removeBtn) return;
            toggleFeedSelection(feed);
        });

        li.addEventListener('keydown', (e) => {
            if (e.target === refreshBtn || e.target === removeBtn) return;
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                toggleFeedSelection(feed);
            }
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

        const bookmarkBtn = template.querySelector('.bookmark-btn');
        bookmarkBtn.textContent = article.isBookmarked ? '\u2605' : '\u2606';
        bookmarkBtn.title = article.isBookmarked ? 'Remove bookmark' : 'Bookmark';
        bookmarkBtn.addEventListener('click', function (e) {
            e.preventDefault();
            toggleBookmark(article.id).then(function (bookmarked) {
                article.isBookmarked = bookmarked;
                bookmarkBtn.textContent = bookmarked ? '\u2605' : '\u2606';
                bookmarkBtn.title = bookmarked ? 'Remove bookmark' : 'Bookmark';
            });
        });

        titleLinkEl.textContent = article.title;
        titleLinkEl.href = article.url;

        const audioEl = template.querySelector('.article-audio');
        if (article.enclosureUrl) {
            audioEl.src = article.enclosureUrl;
            audioEl.type = article.enclosureType || 'audio/mpeg';
            audioEl.classList.remove('hidden');
        }

        if (isRtl(article.title) || isRtl(article.summary)) {
            articleEl.setAttribute('dir', 'rtl');
            summaryEl.setAttribute('dir', 'rtl');
        } else {
            articleEl.setAttribute('dir', 'ltr');
            summaryEl.setAttribute('dir', 'ltr');
        }

        summaryEl.innerHTML = article.summary;
        summaryEl.querySelectorAll('a').forEach(a => {
            a.setAttribute('target', '_blank');
            a.setAttribute('rel', 'noopener noreferrer');
        });

        elements.articleList.appendChild(template);
    });

    renderDigest();
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

function renderFeedUrl() {
    if (state.token && state.handle) {
        var url = window.location.protocol + '//' + window.location.host + '/api/feed/' + state.handle;
        elements.feedUrlDisplay.value = url;
        elements.feedUrlLabel.textContent = t('myFeedUrl');
        elements.copyFeedUrlBtn.textContent = t('copyUrl');
        elements.feedUrlSection.classList.remove('hidden');
    } else {
        elements.feedUrlSection.classList.add('hidden');
    }
}

async function loadPosts() {
    state.showingPosts = true;
    elements.postsView.classList.remove('hidden');
    elements.articleList.innerHTML = '';
    elements.loading.classList.add('hidden');
    elements.empty.classList.add('hidden');
    elements.dailyDigest.classList.add('hidden');
    elements.summarizeBanner.classList.add('hidden');
    elements.error.classList.add('hidden');
    elements.pagination.classList.add('hidden');
    renderFeedUrl();

    try {
        var response = await apiFetch('/api/posts');
        var data = await response.json();
        state.posts = data || [];
        renderFeeds();
        renderPosts();
    } catch (err) {
        elements.postEmpty.innerHTML = '<p>' + tError(err.message) + '</p>';
        elements.postEmpty.classList.remove('hidden');
    }
}

function renderPosts() {
    elements.postList.innerHTML = '';
    elements.postEmpty.classList.add('hidden');

    if (state.posts.length === 0) {
        elements.postEmpty.innerHTML = '<p>' + t('noPosts') + '</p>';
        elements.postEmpty.classList.remove('hidden');
        return;
    }

    state.posts.forEach(function (post) {
        var template = elements.postItemTemplate.content.cloneNode(true);
        var div = template.querySelector('.post-item');
        var titleEl = template.querySelector('.post-item-title');
        var dateEl = template.querySelector('.post-item-date');
        var contentEl = template.querySelector('.post-item-content');
        var editBtn = template.querySelector('.post-edit-btn');
        var deleteBtn = template.querySelector('.post-delete-btn');

        titleEl.textContent = post.title;
        dateEl.textContent = formatDate(post.publishedAt);
        contentEl.textContent = post.content;

        editBtn.textContent = t('editPost');
        deleteBtn.textContent = t('deletePost');

        editBtn.addEventListener('click', function () {
            editPost(post);
        });

        deleteBtn.addEventListener('click', function () {
            deletePost(post.id);
        });

        elements.postList.appendChild(template);
    });
}

function showPostEditor(post) {
    state.showingPosts = true;
    elements.articleList.innerHTML = '';
    elements.loading.classList.add('hidden');
    elements.empty.classList.add('hidden');
    elements.dailyDigest.classList.add('hidden');
    elements.summarizeBanner.classList.add('hidden');
    elements.error.classList.add('hidden');
    elements.pagination.classList.add('hidden');
    elements.postsView.classList.remove('hidden');
    elements.postEditor.classList.remove('hidden');
    renderFeeds();
    if (post) {
        elements.postTitleInput.value = post.title;
        elements.postContentInput.value = post.content;
        state.editingPostId = post.id;
    } else {
        elements.postTitleInput.value = '';
        elements.postContentInput.value = '';
        state.editingPostId = null;
    }
    elements.postTitleInput.focus();
}

function hidePostEditor() {
    elements.postEditor.classList.add('hidden');
    elements.postTitleInput.value = '';
    elements.postContentInput.value = '';
    state.editingPostId = null;
}

async function handlePostSubmit(e) {
    e.preventDefault();
    var title = elements.postTitleInput.value.trim();
    var content = elements.postContentInput.value.trim();
    if (!title) return;

    try {
        if (state.editingPostId) {
            await apiFetch('/api/posts/' + state.editingPostId, {
                method: 'PUT',
                body: JSON.stringify({ title: title, content: content })
            });
        } else {
            await apiFetch('/api/posts', {
                method: 'POST',
                body: JSON.stringify({ title: title, content: content })
            });
        }
        hidePostEditor();
        await loadPosts();
    } catch (err) {
        showError(tError(err.message));
    }
}

function editPost(post) {
    showPostEditor(post);
}

async function deletePost(postId) {
    if (!confirm(t('deletePostConfirm'))) return;

    try {
        await apiFetch('/api/posts/' + postId, { method: 'DELETE' });
        await loadPosts();
    } catch (err) {
        showError(tError(err.message));
    }
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
    elements.summarizeBanner.classList.add('hidden');
    elements.dailyDigest.classList.add('hidden');
    elements.articleList.innerHTML = '';
    elements.postsView.classList.add('hidden');
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
