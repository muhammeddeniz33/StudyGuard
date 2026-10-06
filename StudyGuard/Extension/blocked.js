async function loadStudyGuardConfig() {
    try {

        const response = await fetch(
            "http://127.0.0.1:8765/config"
        );

        if (!response.ok) {
            throw new Error("Config alınamadı.");
        }

        const config = await response.json();

        const messageElement =
            document.getElementById("dynamicMessage");

        messageElement.textContent =
            config.message;

    } catch (error) {

        console.error(
            "StudyGuard config alınamadı:",
            error
        );

        document.getElementById(
            "dynamicMessage"
        ).textContent =
            "Ders modunda bu siteye erişemezsin.";
    }
}

loadStudyGuardConfig();