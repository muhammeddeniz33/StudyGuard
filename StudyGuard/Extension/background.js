const allowedDomains = [
    "tongucakademi.com",
    "eba.gov.tr"
];


// --------------------------------------------------
// TARAYICI / EDGE GÜRÜLTÜSÜ
//
// Bunlar kullanıcı ihlali olarak değerlendirilmez.
// DİKKAT:
// bing.com gibi genel siteleri buraya koymuyoruz.
// --------------------------------------------------

const ignoredSystemDomains = [
    "ntp.msn.com",
    "go.microsoft.com"
];


// --------------------------------------------------
// DOMAIN HELPERS
// --------------------------------------------------

function getHost(url) {

    try {

        return new URL(url)
            .hostname
            .toLowerCase();

    }
    catch {

        return "";
    }
}


function isSystemUrl(url) {

    try {

        const parsed =
            new URL(url);

        return (
            parsed.protocol === "chrome-extension:" ||
            parsed.protocol === "edge:" ||
            parsed.protocol === "chrome:" ||
            parsed.protocol === "about:"
        );

    }
    catch {

        return true;
    }
}


function isIgnoredSystemDomain(url) {

    const host =
        getHost(url);

    return ignoredSystemDomains.some(
        domain =>
            host === domain ||
            host.endsWith("." + domain)
    );
}


function isAllowed(url) {

    if (isSystemUrl(url))
        return true;

    const host =
        getHost(url);

    return allowedDomains.some(
        domain =>
            host === domain ||
            host.endsWith("." + domain)
    );
}


// --------------------------------------------------
// STUDYGUARD CONFIG
// --------------------------------------------------

async function getStudyGuardConfig() {

    try {

        const response =
            await fetch(
                "http://127.0.0.1:8765/config"
            );

        if (!response.ok) {
            throw new Error();
        }

        return await response.json();

    }
    catch {

        // Agent kapalıysa güvenli varsayım:
        // ders modu açık.
        return {
            studyModeEnabled: true
        };
    }
}


// --------------------------------------------------
// C# AGENT'A İHLAL GÖNDER
// --------------------------------------------------

function notifyStudyGuard(url) {

    const endpoint =
        "http://127.0.0.1:8765/violation?url="
        + encodeURIComponent(url);

    fetch(endpoint)
        .catch(() => {
        });
}


// --------------------------------------------------
// NAVIGATION
// --------------------------------------------------

async function handleNavigation(details) {

    // Sadece ana frame.
    // iframe/reklam/resource istekleri dikkate alınmaz.
    if (details.frameId !== 0)
        return;


    // Browser'ın kendi sayfaları
    if (isSystemUrl(details.url))
        return;


    // Edge altyapı gürültüsü.
    // Alarm/log üretmiyoruz.
    if (isIgnoredSystemDomain(details.url)) {

        console.log(
            "StudyGuard ignored browser request:",
            details.url
        );

        return;
    }


    const config =
        await getStudyGuardConfig();


    // Ders modu kapalıysa serbest.
    if (!config.studyModeEnabled) {

        console.log(
            "StudyGuard: Study mode disabled."
        );

        return;
    }


    // İzinli site
    if (isAllowed(details.url))
        return;


    // --------------------------------------------------
    // GERÇEK İHLAL
    // --------------------------------------------------

    console.log(
        "StudyGuard blocked:",
        details.url
    );


    // C# uygulamasına bildir
    notifyStudyGuard(
        details.url
    );


    // Engel sayfasına yönlendir
    chrome.tabs.update(
        details.tabId,
        {
            url:
                chrome.runtime.getURL(
                    "blocked.html"
                )
        }
    );
}


chrome.webNavigation
    .onBeforeNavigate
    .addListener(
        details => {
            handleNavigation(details);
        }
    );