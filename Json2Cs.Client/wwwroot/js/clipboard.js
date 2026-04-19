window.copyToClipboard = (text) => {
    if (!text) {
        return;
    }

    navigator.clipboard.writeText(text).catch((error) => {
        console.error('Clipboard write failed', error);
    });
};

// Monaco Editor initialization
window.initializeMonacoEditor = async (containerId, initialValue, language, editable) => {
    return new Promise((resolve) => {
        const checkMonaco = () => {
            if (window.monaco) {
                const container = document.getElementById(containerId);
                if (!container) {
                    console.error(`Container ${containerId} not found`);
                    resolve(null);
                    return;
                }

                const editor = monaco.editor.create(container, {
                    value: initialValue,
                    language: language,
                    theme: 'vs-dark',
                    fontSize: 14,
                    lineHeight: 1.5,
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    automaticLayout: true,
                    readOnly: !editable,
                    wordWrap: 'on',
                    renderWhitespace: 'selection',
                    bracketPairColorization: { enabled: true },
                    guides: {
                        bracketPairs: true,
                        indentation: true
                    }
                });

                if (editable) {
                    // For JSON editor, listen to changes and call C# method
                    editor.onDidChangeModelContent(() => {
                        const value = editor.getValue();
                        DotNet.invokeMethodAsync('Json2Cs.Client', 'OnJsonInputChanged', value);
                    });
                }

                resolve({
                    dispose: () => editor.dispose(),
                    setValue: (value) => editor.setValue(value),
                    getValue: () => editor.getValue()
                });
            } else {
                setTimeout(checkMonaco, 100);
            }
        };
        checkMonaco();
    });
};

window.updateMonacoEditor = (editorRef, value) => {
    if (editorRef && editorRef.setValue) {
        editorRef.setValue(value);
    }
};
