window.copyToClipboard = (text) => {
    if (!text) {
        return;
    }

    navigator.clipboard.writeText(text).catch((error) => {
        console.error('Clipboard write failed', error);
    });
};

window.updateStatusDisplay = (message) => {
    const statusElement = document.getElementById('status-display');
    if (statusElement) {
        statusElement.textContent = message;
    }
};

// Monaco Editor initialization
window.initializeMonacoEditor = async (containerId, initialValue, language, editable, dotnetRef) => {
    return new Promise((resolve) => {
        const checkMonaco = () => {
            if (window.monaco) {
                const container = document.getElementById(containerId);
                if (!container) {
                    console.error(`Container ${containerId} not found`);
                    resolve(null);
                    return;
                }

                const placeholder = document.createElement('div');
                placeholder.className = 'editor-placeholder';
                placeholder.textContent = language === 'json' ? '// JSON hier einfügen...' : '';
                container.appendChild(placeholder);

                const editor = monaco.editor.create(container, {
                    value: initialValue || '',
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

                const updatePlaceholder = () => {
                    if (language === 'json') {
                        placeholder.style.display = editor.getValue().length === 0 ? 'block' : 'none';
                    } else {
                        placeholder.style.display = 'none';
                    }
                };

                updatePlaceholder();
                editor.onDidChangeModelContent(() => {
                    updatePlaceholder();
                    if (editable && dotnetRef) {
                        const value = editor.getValue();
                        dotnetRef.invokeMethodAsync('OnJsonChanged', value);
                    }
                });

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
