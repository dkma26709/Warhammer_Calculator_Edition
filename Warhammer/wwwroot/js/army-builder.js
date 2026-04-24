window.armyBuilder = {
    downloadJson: function (filename, jsonText) {
        const blob = new Blob([jsonText], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },
    saveLocal: function (key, value) {
        try { localStorage.setItem(key, value); } catch (_) { }
    },
    loadLocal: function (key) {
        try { return localStorage.getItem(key); } catch (_) { return null; }
    },
    clearLocal: function (key) {
        try { localStorage.removeItem(key); } catch (_) { }
    }
};
