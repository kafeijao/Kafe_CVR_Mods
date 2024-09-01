const MenuCSSLoaderMod = {

    init: function() {
        engine.on("MenuCSSLoaderLoadCSSFile", MenuCSSLoaderMod.loadCssFile);
        engine.on("MenuCSSLoaderLoadCSSText", MenuCSSLoaderMod.loadCssText);
        engine.trigger('MenuCSSLoaderInitialized');
    },

    loadCssFile: function(cssPathFile) {
        console.log('[MenuCSSLoaderMod] Loading the CSS file on the path: ', cssPathFile);

        const link = document.createElement("link");
        link.rel = "stylesheet";
        link.type = "text/css";
        link.href = cssPathFile;
        document.getElementsByTagName("head")[0].appendChild(link);
    },

    loadCssText: function(cssText) {
        console.log('[MenuCSSLoaderMod] Loading CSS text: ', cssText);

        const style = document.createElement("style");
        style.type = "text/css";
        style.appendChild(document.createTextNode(cssText));
        document.getElementsByTagName("head")[0].appendChild(style);
    }
};

MenuCSSLoaderMod.init();

console.log('MenuCSSLoaderMod has been initialized!');
