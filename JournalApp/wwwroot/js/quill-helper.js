// Quill Editor Helper Functions

window.initQuill = function (selector) {
    const container = document.querySelector(selector);
    if (!container) {
        console.error('Quill container not found:', selector);
        return null;
    }

    const quill = new Quill(selector, {
        theme: 'snow',
        modules: {
            toolbar: [
                [{ 'header': [1, 2, 3, false] }],
                ['bold', 'italic', 'underline', 'strike'],
                [{ 'list': 'ordered' }, { 'list': 'bullet' }],
                [{ 'indent': '-1' }, { 'indent': '+1' }],
                ['link'],
                [{ 'align': [] }],
                ['clean']
            ]
        },
        placeholder: 'Write your thoughts here...'
    });

    return quill;
};

window.getQuillHtml = function (quill) {
    if (!quill) return '';
    return quill.root.innerHTML;
};

window.getQuillText = function (quill) {
    if (!quill) return '';
    return quill.getText();
};

window.setQuillContent = function (quill, html) {
    if (quill && html) {
        quill.root.innerHTML = html;
    }
};

window.getQuillWordCount = function (quill) {
    if (!quill) return 0;
    const text = quill.getText().trim();
    if (!text) return 0;
    return text.split(/\s+/).length;
};