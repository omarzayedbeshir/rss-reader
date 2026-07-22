const translations = {
    en: {
        authTitle: "RSS Reader",
        authSubtitle: "Sign in or create an account",
        emailPlaceholder: "Email",
        passwordPlaceholder: "Password",
        signIn: "Sign In",
        signUp: "Sign Up",
        createAccount: "Create an account",
        signInInstead: "Sign in instead",
        guestContinue: "Continue without signing in",
        emailPasswordRequired: "Email and password are required.",
        verificationSent: function (email) { return "Verification email sent to " + email + ". Check your inbox and click the link to verify."; },
        emailVerified: "Email verified! You can now sign in.",
        invalidVerification: "Invalid or expired verification link.",
        resendVerification: "Resend verification email",
        signOut: "Sign Out",
        refreshAll: "Refresh All",
        addFeed: "Add Feed",
        feedUrlPlaceholder: "https://example.com/rss",
        subscribe: "Subscribe",
        cancel: "Cancel",
        loadingArticles: "Loading articles...",
        noFeeds: "No feeds subscribed yet.",
        clickAddFeed: "Click \"Add Feed\" to get started.",
        removeFeedConfirm: "Remove this feed and all its articles?",
        refreshFeedTitle: "Refresh feed",
        removeFeedTitle: "Remove feed",
        prev: "Prev",
        next: "Next",
        pageOf: function (p, t) { return "Page " + p + " of " + t; },
        justNow: "Just now",
        minutesAgo: function (m) { return m + "m ago"; },
        hoursAgo: function (h) { return h + "h ago"; },
        unknown: "Unknown",
        langLabel: "\u0627\u0644\u0639\u0631\u0628\u064a\u0629",
        myPosts: "My Posts",
        postsView: "Posts",
        newPost: "New Post",
        editPost: "Edit",
        deletePost: "Delete",
        postTitle: "Title",
        postContent: "Content",
        save: "Save",
        cancel: "Cancel",
        deletePostConfirm: "Delete this post?",
        myFeedUrl: "My Feed URL",
        copyUrl: "Copy",
        urlCopied: "Copied!",
        noPosts: "No posts yet.",
        errors: {
            "Invalid email or password.": "Invalid email or password.",
            "Email not verified. Check your inbox or request a new verification email.": "Email not verified. Check your inbox or request a new verification email.",
            "Email already registered.": "Email already registered.",
            "A valid email is required.": "A valid email is required.",
            "Password must be at least 4 characters.": "Password must be at least 4 characters.",
            "Email is required.": "Email is required.",
            "Email is already verified.": "Email is already verified.",
            "Not authenticated.": "Not authenticated."
        }
    },
    ar: {
        authTitle: "\u0642\u0627\u0631\u0626 RSS",
        authSubtitle: "\u0633\u062c\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0623\u0648 \u0623\u0646\u0634\u0626 \u062d\u0633\u0627\u0628\u064b\u0627",
        emailPlaceholder: "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a",
        passwordPlaceholder: "\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631",
        signIn: "\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644",
        signUp: "\u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628",
        createAccount: "\u0625\u0646\u0634\u0627\u0621 \u062d\u0633\u0627\u0628 \u062c\u062f\u064a\u062f",
        signInInstead: "\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644 \u0628\u062f\u0644\u0627\u064b \u0645\u0646 \u0630\u0644\u0643",
        guestContinue: "\u0645\u062a\u0627\u0628\u0639\u0629 \u0628\u062f\u0648\u0646 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644",
        emailPasswordRequired: "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0648\u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0645\u0637\u0644\u0648\u0628\u0627\u0646.",
        verificationSent: function (email) { return "\u062a\u0645 \u0625\u0631\u0633\u0627\u0644 \u0628\u0631\u064a\u062f \u0627\u0644\u062a\u062d\u0642\u0642 \u0625\u0644\u0649 " + email + ". \u062a\u062d\u0642\u0642 \u0645\u0646 \u0635\u0646\u062f\u0648\u0642 \u0627\u0644\u0648\u0627\u0631\u062f \u0648\u0627\u0636\u063a\u0637 \u0639\u0644\u0649 \u0627\u0644\u0631\u0627\u0628\u0637 \u0644\u0644\u062a\u062d\u0642\u0642."; },
        emailVerified: "\u062a\u0645 \u0627\u0644\u062a\u062d\u0642\u0642 \u0645\u0646 \u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a! \u064a\u0645\u0643\u0646\u0643 \u0627\u0644\u0622\u0646 \u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062f\u062e\u0648\u0644.",
        invalidVerification: "\u0631\u0627\u0628\u0637 \u0627\u0644\u062a\u062d\u0642\u0642 \u063a\u064a\u0631 \u0635\u0627\u0644\u062d \u0623\u0648 \u0645\u0646\u062a\u0647\u064a \u0627\u0644\u0635\u0644\u0627\u062d\u064a\u0629.",
        resendVerification: "\u0625\u0639\u0627\u062f\u0629 \u0625\u0631\u0633\u0627\u0644 \u0628\u0631\u064a\u062f \u0627\u0644\u062a\u062d\u0642\u0642",
        signOut: "\u062a\u0633\u062c\u064a\u0644 \u0627\u0644\u062e\u0631\u0648\u062c",
        refreshAll: "\u062a\u062d\u062f\u064a\u062b \u0627\u0644\u0643\u0644",
        addFeed: "\u0625\u0636\u0627\u0641\u0629 \u062e\u0644\u0627\u0635\u0629",
        feedUrlPlaceholder: "https://example.com/rss",
        subscribe: "\u0627\u0634\u062a\u0631\u0627\u0643",
        cancel: "\u0625\u0644\u063a\u0627\u0621",
        loadingArticles: "\u062c\u0627\u0631\u064d \u062a\u062d\u0645\u064a\u0644 \u0627\u0644\u0645\u0642\u0627\u0644\u0627\u062a...",
        noFeeds: "\u0644\u0627 \u062a\u0648\u062c\u062f \u062e\u0644\u0627\u0635\u0627\u062a \u0645\u0634\u062a\u0631\u0643 \u0628\u0647\u0627 \u0628\u0639\u062f.",
        clickAddFeed: "\u0627\u0636\u063a\u0637 \"\u0625\u0636\u0627\u0641\u0629 \u062e\u0644\u0627\u0635\u0629\" \u0644\u0644\u0628\u062f\u0621.",
        removeFeedConfirm: "\u0625\u0632\u0627\u0644\u0629 \u0647\u0630\u0647 \u0627\u0644\u062e\u0644\u0627\u0635\u0629 \u0648\u062c\u0645\u064a\u0639 \u0645\u0642\u0627\u0644\u0627\u062a\u0647\u0627\u061f",
        refreshFeedTitle: "\u062a\u062d\u062f\u064a\u062b \u0627\u0644\u062e\u0644\u0627\u0635\u0629",
        removeFeedTitle: "\u0625\u0632\u0627\u0644\u0629 \u0627\u0644\u062e\u0644\u0627\u0635\u0629",
        prev: "\u0627\u0644\u0633\u0627\u0628\u0642",
        next: "\u0627\u0644\u062a\u0627\u0644\u064a",
        pageOf: function (p, t) { return "\u0635\u0641\u062d\u0629 " + p + " \u0645\u0646 " + t; },
        justNow: "\u0627\u0644\u0622\u0646",
        minutesAgo: function (m) { return "\u0645\u0646\u0630 " + m + " \u062f\u0642\u064a\u0642\u0629"; },
        hoursAgo: function (h) { return "\u0645\u0646\u0630 " + h + " \u0633\u0627\u0639\u0629"; },
        unknown: "\u063a\u064a\u0631 \u0645\u0639\u0631\u0648\u0641",
        langLabel: "English",
        myPosts: "\u0645\u0646\u0634\u0648\u0631\u0627\u062a\u064a",
        postsView: "\u0627\u0644\u0645\u0646\u0634\u0648\u0631\u0627\u062a",
        newPost: "\u0645\u0646\u0634\u0648\u0631 \u062c\u062f\u064a\u062f",
        editPost: "\u062a\u0639\u062f\u064a\u0644",
        deletePost: "\u062d\u0630\u0641",
        postTitle: "\u0627\u0644\u0639\u0646\u0648\u0627\u0646",
        postContent: "\u0627\u0644\u0645\u062d\u062a\u0648\u0649",
        save: "\u062d\u0641\u0638",
        cancel: "\u0625\u0644\u063a\u0627\u0621",
        deletePostConfirm: "\u062d\u0630\u0641 \u0647\u0630\u0627 \u0627\u0644\u0645\u0646\u0634\u0648\u0631\u061f",
        myFeedUrl: "\u0631\u0627\u0628\u0637 \u062a\u063a\u0630\u064a\u062a\u064a",
        copyUrl: "\u0646\u0633\u062e",
        urlCopied: "\u062a\u0645 \u0627\u0644\u0646\u0633\u062e!",
        noPosts: "\u0644\u0627 \u062a\u0648\u062c\u062f \u0645\u0646\u0634\u0648\u0631\u0627\u062a \u0628\u0639\u062f.",
        errors: {
            "Invalid email or password.": "\u0628\u0631\u064a\u062f \u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0623\u0648 \u0643\u0644\u0645\u0629 \u0645\u0631\u0648\u0631 \u063a\u064a\u0631 \u0635\u062d\u064a\u062d\u0629.",
            "Email not verified. Check your inbox or request a new verification email.": "\u0644\u0645 \u064a\u062a\u0645 \u0627\u0644\u062a\u062d\u0642\u0642 \u0645\u0646 \u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a. \u062a\u062d\u0642\u0642 \u0645\u0646 \u0635\u0646\u062f\u0648\u0642 \u0627\u0644\u0648\u0627\u0631\u062f \u0623\u0648 \u0627\u0637\u0644\u0628 \u0628\u0631\u064a\u062f \u062a\u062d\u0642\u0642 \u062c\u062f\u064a\u062f.",
            "Email already registered.": "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0645\u0633\u062c\u0644 \u0628\u0627\u0644\u0641\u0639\u0644.",
            "A valid email is required.": "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0635\u0627\u0644\u062d \u0645\u0637\u0644\u0648\u0628.",
            "Password must be at least 4 characters.": "\u064a\u062c\u0628 \u0623\u0646 \u062a\u062a\u0643\u0648\u0646 \u0643\u0644\u0645\u0629 \u0627\u0644\u0645\u0631\u0648\u0631 \u0645\u0646 4 \u0623\u062d\u0631\u0641 \u0639\u0644\u0649 \u0627\u0644\u0623\u0642\u0644.",
            "Email is required.": "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0645\u0637\u0644\u0648\u0628.",
            "Email is already verified.": "\u0627\u0644\u0628\u0631\u064a\u062f \u0627\u0644\u0625\u0644\u0643\u062a\u0631\u0648\u0646\u064a \u0645\u0624\u0643\u062f \u0628\u0627\u0644\u0641\u0639\u0644.",
            "Not authenticated.": "\u063a\u064a\u0631 \u0645\u0635\u062f\u0642."
        }
    }
};

var lang = localStorage.getItem('lang')
    || (navigator.language && navigator.language.startsWith('ar') ? 'ar' : 'en');

function t(key) {
    var map = translations[lang] || translations.en;
    var val = map[key];
    if (typeof val === 'function') {
        return val.apply(null, Array.prototype.slice.call(arguments, 1));
    }
    return val !== undefined ? val : ((translations.en[key] !== undefined) ? translations.en[key] : key);
}

function tError(serverMessage) {
    var map = translations[lang] || translations.en;
    if (map.errors && map.errors[serverMessage]) return map.errors[serverMessage];
    if (translations.en.errors && translations.en.errors[serverMessage]) return translations.en.errors[serverMessage];
    return serverMessage;
}

function setPageDirection() {
    document.documentElement.lang = lang;
    document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
}

function toggleLang() {
    lang = lang === 'en' ? 'ar' : 'en';
    localStorage.setItem('lang', lang);
    setPageDirection();
    location.reload();
}

setPageDirection();
