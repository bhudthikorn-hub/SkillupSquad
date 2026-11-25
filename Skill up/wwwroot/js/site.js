document.addEventListener('DOMContentLoaded', () => {
    const menuButton = document.querySelector('[data-menu-toggle]');
    const menu = document.querySelector('[data-menu]');
    menuButton?.addEventListener('click', () => menu?.classList.toggle('open'));

    const themeToggle = document.querySelector('[data-theme-toggle]');
    const setThemeState = () => {
        const isDark = document.documentElement.classList.contains('dark-theme');
        if (themeToggle) {
            themeToggle.classList.toggle('is-dark', isDark);
            themeToggle.querySelector('span').textContent = isDark ? '☾' : '☀';
            themeToggle.setAttribute('aria-pressed', isDark.toString());
        }
    };
    if (localStorage.getItem('skillup-theme') === 'dark') {
        document.documentElement.classList.add('dark-theme');
    }
    setThemeState();
    themeToggle?.addEventListener('click', () => {
        document.documentElement.classList.toggle('dark-theme');
        localStorage.setItem('skillup-theme', document.documentElement.classList.contains('dark-theme') ? 'dark' : 'light');
        setThemeState();
    });

    const removeCourseModal = document.querySelector('[data-remove-course-modal]');
    const removeCourseName = removeCourseModal?.querySelector('[data-remove-course-name]');
    const removeCourseConfirm = removeCourseModal?.querySelector('[data-remove-course-confirm]');
    let removeCourseForm;

    const closeRemoveCourseModal = () => {
        if (!removeCourseModal) return;
        removeCourseModal.hidden = true;
        removeCourseForm = null;
    };

    document.querySelectorAll('[data-remove-course-form]').forEach(form => {
        form.addEventListener('submit', event => {
            if (!removeCourseModal || !removeCourseConfirm) return;
            event.preventDefault();
            removeCourseForm = form;
            if (removeCourseName) {
                removeCourseName.textContent = form.dataset.courseName || 'คอร์สนี้';
            }
            removeCourseModal.hidden = false;
            removeCourseConfirm.focus();
        });
    });

    removeCourseModal?.querySelectorAll('[data-remove-course-cancel]').forEach(button => {
        button.addEventListener('click', closeRemoveCourseModal);
    });

    removeCourseConfirm?.addEventListener('click', () => {
        if (removeCourseForm) {
            removeCourseForm.submit();
        }
    });

    removeCourseModal?.addEventListener('keydown', event => {
        if (event.key === 'Escape') closeRemoveCourseModal();
    });

    const searchInput = document.querySelector('[data-site-search]');
    const searchResults = document.querySelector('[data-search-results]');
    const searchItems = [
        ['Graphic Design', '/Home/Category?skill=Graphic%20Design'],
        ['3D & Animation', '/Home/Category?skill=3D%20%26%20Animation'],
        ['Video Editing', '/Home/Category?skill=Video%20Editing'],
        ['UX/UI', '/Home/Category?skill=UX%2FUI'],
        ['Web Development', '/Home/Category?skill=Web%20Development'],
        ['Digital Marketing', '/Home/Category?skill=Digital%20Marketing'],
        ['Explore Skills', '/Home/Explore'],
        ['My Learning', '/Home/Learning'],
        ['Profile', '/Home/Profile']
    ];
    const hideSearchResults = () => {
        if (searchResults) {
            searchResults.hidden = true;
            searchResults.replaceChildren();
        }
    };
    searchInput?.addEventListener('input', () => {
        const query = searchInput.value.trim().toLowerCase();
        if (!searchResults || !query) {
            hideSearchResults();
            return;
        }
        const matches = searchItems.filter(([label]) => label.toLowerCase().includes(query));
        searchResults.replaceChildren();
        if (!matches.length) {
            const empty = document.createElement('span');
            empty.className = 'search-result-empty';
            empty.textContent = 'ไม่พบหัวข้อที่ค้นหา';
            searchResults.append(empty);
        } else {
            matches.forEach(([label, url]) => {
                const link = document.createElement('a');
                link.href = url;
                link.textContent = label;
                searchResults.append(link);
            });
        }
        searchResults.hidden = false;
    });
    document.addEventListener('click', event => {
        if (!event.target.closest('.framework-search')) {
            hideSearchResults();
        }
    });

    const adminSearch = document.querySelector('[data-admin-search]');
    const adminSearchResults = document.querySelector('[data-admin-search-results]');
    const adminPageAliases = {
        user: '/Admin/Users', learner: '/Admin/Users', 'ผู้ใช้': '/Admin/Users', 'ผู้เรียน': '/Admin/Users',
        course: '/Admin/Courses', 'คอร์ส': '/Admin/Courses', 'บทเรียน': '/Admin/Courses',
        quiz: '/Admin/Quizzes', test: '/Admin/Quizzes', 'ข้อสอบ': '/Admin/Quizzes', 'แบบทดสอบ': '/Admin/Quizzes',
        dash: '/Admin/Dashboard', 'สถิติ': '/Admin/Dashboard', 'กราฟ': '/Admin/Dashboard'
    };

    const closeAdminResults = () => {
        if (adminSearchResults) {
            adminSearchResults.hidden = true;
            adminSearchResults.replaceChildren();
        }
    };

    const getAdminRows = () => [...document.querySelectorAll('[data-admin-result]')];
    const getAdminTableSearch = () => document.querySelector('.admin-content .table-search input');
    const filterAdminRows = query => {
        const normalized = query.trim().toLocaleLowerCase();
        getAdminRows().forEach(row => {
            const haystack = [row.dataset.adminLabel, row.dataset.adminEmail, row.textContent]
                .filter(Boolean).join(' ').toLocaleLowerCase();
            row.hidden = Boolean(normalized) && !haystack.includes(normalized);
        });
    };

    const showAdminSuggestions = query => {
        if (!adminSearchResults) return;
        const normalized = query.trim().toLocaleLowerCase();
        adminSearchResults.replaceChildren();
        if (!normalized) {
            closeAdminResults();
            return;
        }

        const matches = getAdminRows().filter(row => {
            const text = [row.dataset.adminLabel, row.dataset.adminEmail, row.textContent]
                .filter(Boolean).join(' ').toLocaleLowerCase();
            return text.includes(normalized);
        }).slice(0, 6);

        if (!matches.length) {
            const empty = document.createElement('span');
            empty.className = 'admin-search-empty';
            empty.textContent = 'ไม่พบรายการในหน้าปัจจุบัน';
            adminSearchResults.append(empty);
        } else {
            matches.forEach(row => {
                const link = document.createElement('a');
                const kind = row.dataset.adminKind === 'learner' ? 'Learner' : row.dataset.adminKind === 'quiz' ? 'Quiz' : 'Course';
                link.href = row.dataset.adminUrl;
                const category = row.dataset.adminCategory ? ` <em>(${row.dataset.adminCategory})</em>` : '';
                link.innerHTML = `<small>[${kind}]</small> ${row.dataset.adminLabel || row.textContent.trim()}${category}`;
                adminSearchResults.append(link);
            });
        }
        adminSearchResults.hidden = false;
    };

    const navigateAdminAlias = query => {
        const target = adminPageAliases[query.trim().toLocaleLowerCase()];
        if (target) {
            window.location.href = target;
            return true;
        }
        return false;
    };

    adminSearch?.addEventListener('input', () => {
        const query = adminSearch.value;
        const tableSearch = getAdminTableSearch();
        if (tableSearch && tableSearch !== adminSearch) {
            tableSearch.value = query;
            tableSearch.dispatchEvent(new Event('input', { bubbles: true }));
        }
        filterAdminRows(query);
        showAdminSuggestions(query);
    });

    adminSearch?.addEventListener('keydown', event => {
        if (event.key === 'Enter' && navigateAdminAlias(adminSearch.value)) event.preventDefault();
    });

    getAdminTableSearch()?.addEventListener('input', event => {
        if (adminSearch && event.target !== adminSearch) adminSearch.value = event.target.value;
        filterAdminRows(event.target.value);
        showAdminSuggestions(event.target.value);
    });

    document.addEventListener('click', event => {
        if (!event.target.closest('.admin-search')) closeAdminResults();
    });

    document.querySelectorAll('.filter-pills button, .skill-tabs button, .learning-tabs button').forEach(button => {
        button.addEventListener('click', () => {
            button.parentElement.querySelectorAll('button').forEach(item => item.classList.remove('selected'));
            button.classList.add('selected');
        });
    });

    document.querySelectorAll('.category-tabs a').forEach(tab => {
        tab.addEventListener('click', () => {
            tab.parentElement.querySelectorAll('a').forEach(item => item.classList.remove('active'));
            tab.classList.add('active');
        });
    });

    document.querySelectorAll('.password-field button').forEach(button => {
        button.addEventListener('click', () => {
            const input = button.parentElement.querySelector('input');
            input.type = input.type === 'password' ? 'text' : 'password';
        });
    });

    const learningPage = document.querySelector('.learning-framework');
    learningPage?.querySelectorAll('[data-learning-tab]').forEach(button => {
        button.addEventListener('click', () => {
            const target = button.dataset.learningTab;
            learningPage.querySelectorAll('[data-learning-tab]').forEach(item => {
                item.classList.toggle('selected', item.dataset.learningTab === target && item.closest('.learning-framework-tabs'));
                item.classList.toggle('active', item.dataset.learningTab === target && item.classList.contains('learning-stat-card'));
            });
            learningPage.querySelectorAll('[data-learning-panel]').forEach(panel => {
                panel.hidden = panel.dataset.learningPanel !== target;
            });
        });
    });

    const quizPage = document.querySelector('.quiz-framework');
    if (quizPage) {
        const questions = [
            { text: 'คุณสนใจทำงานแบบไหนมากที่สุด?', options: [['ออกแบบภาพและสื่อสร้างสรรค์', 'Graphic Design'], ['สร้างเว็บไซต์หรือแอป', 'Web Development'], ['ทำคอนเทนต์และวิดีโอ', 'Video Editing']] },
            { text: 'กิจกรรมไหนที่คุณชอบทำในเวลาว่าง?', options: [['วาดรูป จัดสี หรือแต่งภาพ', 'Graphic Design'], ['ทดลองทำโปรเจกต์ใหม่ ๆ', 'Web Development'], ['ถ่ายคลิป ตัดต่อ หรือเล่าเรื่อง', 'Video Editing']] },
            { text: 'คุณอยากพัฒนาทักษะเพื่ออะไร?', options: [['สร้างผลงานที่สวยและโดดเด่น', 'Graphic Design'], ['ทำงานสายเทคโนโลยี', 'Web Development'], ['สร้างสื่อที่น่าสนใจ', 'Video Editing']] },
            { text: 'รูปแบบการเรียนแบบไหนเหมาะกับคุณ?', options: [['เรียนรู้ผ่านภาพและตัวอย่าง', 'Graphic Design'], ['ลงมือทำและทดลองจริง', 'Web Development'], ['เรียนรู้ผ่านโปรเจกต์และเรื่องราว', 'Video Editing']] },
            { text: 'ผลงานแบบไหนที่คุณอยากสร้าง?', options: [['แบรนด์ โปสเตอร์ หรือภาพประกอบ', 'Graphic Design'], ['เว็บไซต์หรือแอปพลิเคชัน', 'Web Development'], ['วิดีโอและคอนเทนต์ออนไลน์', 'Video Editing']] }
        ];
        const scores = {};
        let current = 0;
        const questionTitle = quizPage.querySelector('[data-quiz-question]');
        const optionsContainer = quizPage.querySelector('[data-quiz-options]');
        const nextButton = quizPage.querySelector('[data-quiz-next]');
        const progress = quizPage.querySelector('.quiz-progress i');
        const card = quizPage.querySelector('[data-quiz-card]');
        const result = quizPage.querySelector('[data-quiz-result]');
        const renderQuestion = () => {
            const question = questions[current];
            quizPage.querySelector('[data-quiz-current]').textContent = current + 1;
            questionTitle.textContent = question.text;
            optionsContainer.innerHTML = question.options.map((option, index) => `<button type="button" data-skill="${option[1]}" class="quiz-option"><span>${index + 1}</span>${option[0]}</button>`).join('');
            nextButton.disabled = true;
            progress.style.width = `${((current + 1) / questions.length) * 100}%`;
            optionsContainer.querySelectorAll('.quiz-option').forEach(option => option.addEventListener('click', () => {
                optionsContainer.querySelectorAll('.quiz-option').forEach(item => item.classList.remove('selected'));
                option.classList.add('selected');
                nextButton.disabled = false;
            }));
        };
        nextButton.addEventListener('click', () => {
            const selected = optionsContainer.querySelector('.selected');
            scores[selected.dataset.skill] = (scores[selected.dataset.skill] || 0) + 1;
            if (current < questions.length - 1) {
                current++;
                renderQuestion();
            } else {
                const winner = Object.entries(scores).sort((a, b) => b[1] - a[1])[0][0];
                const descriptions = { 'Graphic Design': 'คุณเหมาะกับการสร้างงานภาพ แบรนด์ และสื่อที่สวยงาม', 'Web Development': 'คุณเหมาะกับการสร้างเว็บไซต์และประสบการณ์ดิจิทัล', 'Video Editing': 'คุณเหมาะกับการเล่าเรื่องผ่านวิดีโอและคอนเทนต์' };
                card.hidden = true;
                result.hidden = false;
                result.querySelector('[data-quiz-result-title]').textContent = winner;
                result.querySelector('[data-quiz-result-description]').textContent = descriptions[winner];
                result.querySelector('[data-quiz-result-link]').href = `/Home/Category?skill=${encodeURIComponent(winner)}`;
            }
        });
        quizPage.querySelector('[data-quiz-retry]').addEventListener('click', () => { Object.keys(scores).forEach(key => delete scores[key]); current = 0; card.hidden = false; result.hidden = true; renderQuestion(); });
        renderQuestion();
    }
});