const state = {
    feeds: [],
    articles: [],
    selectedFeedId: null,
    loading: false,
    currentPage: 1,
    totalPages: 1,
    pageSize: 20
};

const elements = {
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

async function init() {
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

    await fetchFeeds(1);
}

async function fetchFeeds(page = state.currentPage) {
    showLoading();
    hideError();

    try {
        let url = `/api/feeds?page=${page}&pageSize=${state.pageSize}`;
        if (state.selectedFeedId) {
            url += `&feedId=${state.selectedFeedId}`;
        }

        const response = await fetch(url);
        if (!response.ok) throw new Error(`Server error: ${response.status}`);

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
        const response = await fetch('/api/feeds', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ url })
        });

        if (!response.ok) {
            const err = await response.json();
            throw new Error(err.error || 'Failed to add feed');
        }

        await fetchFeeds(1);
    } catch (err) {
        showError(err.message);
    }
}

async function removeFeed(id) {
    if (!confirm('Remove this feed and all its articles?')) return;

    showLoading();
    hideError();

    try {
        const response = await fetch(`/api/feeds/${id}`, { method: 'DELETE' });

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
        await fetch(`/api/feeds/${id}/refresh`, { method: 'POST' });
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
            fetch(`/api/feeds/${feed.id}/refresh`, { method: 'POST' })
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
        elements.empty.classList.remove('hidden');
        return;
    }

    elements.empty.classList.add('hidden');

    articles.forEach(article => {
        const feed = state.feeds.find(f => f.id === article.feedId);
        const feedTitle = feed ? feed.title : 'Unknown';

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
    elements.pageInfo.textContent = `Page ${state.currentPage} of ${state.totalPages}`;
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

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;

    return date.toLocaleDateString('en-US', {
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
