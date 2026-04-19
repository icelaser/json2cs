window.copyToClipboard = (text) => {
    if (!text) {
        return;
    }

    navigator.clipboard.writeText(text).catch((error) => {
        console.error('Clipboard write failed', error);
    });
};
