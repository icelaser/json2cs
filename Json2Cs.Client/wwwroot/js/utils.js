function initGoogleAnalytics(measurementId) {
    if (!measurementId || measurementId === 'G-XXXXXXXXXX') return;

    const script = document.createElement('script');
    script.async = true;
    script.src = 'https://www.googletagmanager.com/gtag/js?id=' 
        + measurementId;
    document.head.appendChild(script);

    script.onload = function() {
        window.dataLayer = window.dataLayer || [];
        function gtag(){ dataLayer.push(arguments); }
        window.gtag = gtag;
        gtag('js', new Date());
        gtag('config', measurementId);
    };
}