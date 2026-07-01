<script setup>
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useAuthStore } from './stores/auth';
import { apiRequest } from './lib/api';

const auth = useAuthStore();

const books = ref([]);
const dashboard = ref(null);
const readers = ref([]);
const users = ref([]);
const recentTransactions = ref([]);
const readerBorrowings = ref([]);
const fineSummary = ref(null);
const borrowingPolicy = ref(null);
const bookCategories = ref([]);

const loadingBooks = ref(false);
const loadingPanel = ref(false);
const searchText = ref('');
const searchTimer = ref(null);
const selectedCategory = ref('all');
const selectedBook = ref(null);
const loginMode = ref('login');
const showBookForm = ref(false);
const busyAction = ref('');
const errorMessage = ref('');
const successMessage = ref('');
const borrowDays = ref(14);
const borrowDaysOptions = [7, 14, 21, 30];
const borrowDialog = reactive({
  open: false,
  book: null,
  days: 14,
  dueDate: ''
});
const unreadCount = computed(() => Number(dashboard.value?.overdueBorrowings || 0) + Number(readerBorrowings.value.filter(isOverdueBorrowing).length || 0));
const borrowDeadlinePreview = computed(() => addDays(new Date(), Number(borrowDays.value || 14)));
const borrowDialogDueDate = computed(() => parseDateInput(borrowDialog.dueDate) || addDays(new Date(), Number(borrowDialog.days || borrowDays.value || 14)));
const borrowDialogQuickDays = computed(() => (borrowDaysOptions.includes(Number(borrowDialog.days)) ? String(borrowDialog.days) : ''));
const borrowMinDueDate = computed(() => formatDateInput(addDays(new Date(), 1)));
const borrowMaxDueDate = computed(() => formatDateInput(addDays(new Date(), 365)));

const currentYear = new Date().getFullYear();
const dayMs = 24 * 60 * 60 * 1000;

const currencyFormatter = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND',
  maximumFractionDigits: 0
});

const dateFormatter = new Intl.DateTimeFormat('vi-VN', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric'
});

const loginForm = reactive({
  fullName: '',
  email: 'admin@library.local',
  password: 'Admin@123'
});

const bookForm = reactive(createEmptyBookForm());
const policyForm = reactive(createDefaultPolicyForm());
const categoryForm = reactive(createDefaultCategoryForm());
const externalSearch = reactive({
  query: '',
  results: [],
  loading: false
});

const isAdmin = computed(() => auth.role === 'Admin');
const isStaff = computed(() => isAdmin.value || auth.role === 'Librarian');
const isReader = computed(() => auth.role === 'Reader');
const roleLabel = computed(() => {
  if (!auth.isAuthenticated) return 'Khách';
  return auth.role || 'Thành viên';
});

const totalBooks = computed(() => books.value.length);
const totalReaders = computed(() => dashboard.value?.totalReaders ?? readers.value.length ?? 0);
const activeBorrowings = computed(() => dashboard.value?.activeBorrowings ?? readerBorrowings.value.filter(isActiveBorrowing).length);
const overdueBorrowings = computed(() => dashboard.value?.overdueBorrowings ?? readerBorrowings.value.filter(isOverdueBorrowing).length);
const categoryOptions = computed(() =>
  bookCategories.value
    .filter((category) => category.isActive)
    .sort((a, b) => a.name.localeCompare(b.name, 'vi'))
);
const visibleBooks = computed(() => {
  const keyword = searchText.value.trim().toLowerCase();

  return books.value
    .filter((book) => {
      if (selectedCategory.value === 'all') return true;
      if (selectedCategory.value === 'new') return isNewBook(book);
      if (selectedCategory.value === 'available') return book.canBorrow;
      if (selectedCategory.value === 'borrowed') return !book.canBorrow && !book.isArchived;
      return book.category === selectedCategory.value;
    })
    .filter((book) => {
      if (!keyword) return true;
      const haystack = [book.title, book.author, book.publisher, book.category, book.isbn, book.description, book.content]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();
      return haystack.includes(keyword);
    })
    .sort((a, b) => new Date(b.createdAtUtc).getTime() - new Date(a.createdAtUtc).getTime());
});

const categories = computed(() => {
  const unique = Array.from(new Set([
    ...categoryOptions.value.map((category) => category.name),
    ...books.value.map((book) => book.category).filter(Boolean)
  ]));
  return [
    { key: 'all', label: 'Tất cả' },
    { key: 'new', label: 'Mới' },
    { key: 'available', label: 'Có sẵn' },
    { key: 'borrowed', label: 'Đang mượn' },
    ...unique.map((category) => ({ key: category, label: category }))
  ];
});

const recentBorrowingRows = computed(() =>
  [...recentTransactions.value]
    .sort((a, b) => new Date(b.borrowedAtUtc).getTime() - new Date(a.borrowedAtUtc).getTime())
    .slice(0, 8)
);

const staffNotifications = computed(() => {
  if (isStaff.value) {
    return Number(dashboard.value?.overdueBorrowings ?? recentTransactions.value.filter(isOverdueBorrowing).length ?? 0);
  }

  if (isReader.value) {
    return Number(readerBorrowings.value.filter(isOverdueBorrowing).length || 0);
  }

  return unreadCount.value;
});

const accountInitial = computed(() => auth.user?.fullName?.slice(0, 1) || 'U');
const selectedBookFormTitle = computed(() => (bookForm.id ? 'Chỉnh sửa sách' : 'Thêm sách mới'));

function createEmptyBookForm() {
  return {
    id: '',
    isbn: '',
    title: '',
    author: '',
    publisher: '',
    publishedYear: currentYear,
    category: '',
    totalCopies: 1,
    minimumCopies: 1,
    coverImageUrl: '',
    description: '',
    content: ''
  };
}

function createDefaultPolicyForm() {
  return {
    maxActiveBorrowingsPerReader: 5,
    defaultBorrowDays: 14,
    maxRenewalDays: 30,
    finePerOverdueDay: 2000,
    allowReaderSelfCheckout: true
  };
}

function createDefaultCategoryForm() {
  return {
    name: '',
    description: ''
  };
}

function normalizeList(result) {
  if (Array.isArray(result)) return result;
  if (Array.isArray(result?.value)) return result.value;
  return [];
}

function normalizeCategoryList(result) {
  return normalizeList(result)
    .map((category) => {
      if (typeof category === 'string') {
        return {
          id: category,
          name: category,
          description: '',
          isActive: true
        };
      }

      return {
        ...category,
        name: category.name ?? '',
        isActive: category.isActive ?? true
      };
    })
    .filter((category) => category.name);
}

function cloneBookForm(book = null) {
  if (!book) return createEmptyBookForm();
  return {
    id: book.id ?? '',
    isbn: book.isbn ?? '',
    title: book.title ?? '',
    author: book.author ?? '',
    publisher: book.publisher ?? '',
    publishedYear: book.publishedYear ?? currentYear,
    category: book.category ?? '',
    totalCopies: book.totalCopies ?? 1,
    minimumCopies: book.minimumCopies ?? 1,
    coverImageUrl: book.coverImageUrl ?? '',
    description: book.description ?? '',
    content: book.content ?? ''
  };
}

function addDays(date, days) {
  const nextDate = new Date(date);
  nextDate.setDate(nextDate.getDate() + Number(days || 0));
  return nextDate;
}

function startOfLocalDay(date) {
  return new Date(date.getFullYear(), date.getMonth(), date.getDate());
}

function formatDateInput(date) {
  const localDate = new Date(date.getTime() - date.getTimezoneOffset() * 60000);
  return localDate.toISOString().slice(0, 10);
}

function parseDateInput(value) {
  if (!value) return null;
  const [year, month, day] = String(value).split('-').map(Number);
  if (!year || !month || !day) return null;
  return new Date(year, month - 1, day);
}

function daysUntilDateInput(value) {
  const dueDate = parseDateInput(value);
  if (!dueDate) return Number.NaN;
  return Math.ceil((startOfLocalDay(dueDate).getTime() - startOfLocalDay(new Date()).getTime()) / dayMs);
}

function setBorrowDialogDays(value) {
  const days = Number(value || 0);
  borrowDialog.days = Number.isFinite(days) ? days : 0;
  borrowDialog.dueDate = formatDateInput(addDays(new Date(), borrowDialog.days));
}

function syncBorrowDialogDaysFromDueDate() {
  borrowDialog.days = daysUntilDateInput(borrowDialog.dueDate);
}

function overdueDays(record) {
  if (!isOverdueBorrowing(record)) return 0;
  return Math.max(1, Math.ceil((Date.now() - new Date(record.dueAtUtc).getTime()) / dayMs));
}

function remainingBorrowDays(record) {
  if (!isActiveBorrowing(record) || isOverdueBorrowing(record)) return 0;
  return Math.max(0, Math.ceil((new Date(record.dueAtUtc).getTime() - Date.now()) / dayMs));
}

function resetBookForm(book = null) {
  Object.assign(bookForm, cloneBookForm(book));
  ensureBookFormCategory();
}

function resetPolicyForm(policy = borrowingPolicy.value) {
  Object.assign(policyForm, {
    maxActiveBorrowingsPerReader: policy?.maxActiveBorrowingsPerReader ?? 5,
    defaultBorrowDays: policy?.defaultBorrowDays ?? 14,
    maxRenewalDays: policy?.maxRenewalDays ?? 30,
    finePerOverdueDay: policy?.finePerOverdueDay ?? 2000,
    allowReaderSelfCheckout: policy?.allowReaderSelfCheckout ?? true
  });
}

function resetCategoryForm() {
  Object.assign(categoryForm, createDefaultCategoryForm());
}

function ensureBookFormCategory() {
  if (!bookForm.category && categoryOptions.value.length) {
    bookForm.category = categoryOptions.value[0].name;
  }
}

function resetExternalSearch() {
  externalSearch.query = '';
  externalSearch.results = [];
  externalSearch.loading = false;
}

function applyExternalBook(candidate) {
  bookForm.isbn = candidate.isbn || bookForm.isbn;
  bookForm.title = candidate.title || bookForm.title;
  bookForm.author = candidate.author || bookForm.author;
  bookForm.publisher = candidate.publisher || bookForm.publisher;
  bookForm.publishedYear = candidate.publishedYear || bookForm.publishedYear;
  bookForm.coverImageUrl = candidate.coverImageUrl || bookForm.coverImageUrl;
  bookForm.description = candidate.description || bookForm.description;
  bookForm.content = candidate.content || bookForm.content;

  const matchedCategory = categoryOptions.value.find(
    (category) => category.name.toLowerCase() === String(candidate.suggestedCategory || '').toLowerCase()
  );
  if (matchedCategory) {
    bookForm.category = matchedCategory.name;
  } else if (candidate.suggestedCategory && auth.role === 'Admin') {
    categoryForm.name = candidate.suggestedCategory;
    categoryForm.description = `Suggested by ${candidate.source}`;
    setMessage('success', `Đã lấy dữ liệu sách. Thể loại gợi ý "${candidate.suggestedCategory}" chưa có, bạn có thể thêm ngay.`);
    return;
  }

  setMessage('success', `Đã áp dụng dữ liệu từ ${candidate.source}.`);
}

function isActiveBorrowing(record) {
  return record?.status === 'Borrowed' || record?.status === 'Overdue';
}

function isOverdueBorrowing(record) {
  if (!record) return false;
  return record.status === 'Overdue' || (record.status === 'Borrowed' && new Date(record.dueAtUtc).getTime() < Date.now());
}

function isNewBook(book) {
  return (Date.now() - new Date(book.createdAtUtc).getTime()) <= 14 * dayMs;
}

function bookStatusLabel(book) {
  if (book.isArchived) return 'Đã lưu trữ';
  if (book.canBorrow) return isNewBook(book) ? 'Mới' : 'Có sẵn';
  return 'Đang mượn';
}

function bookStatusTone(book) {
  if (book.isArchived) return 'archived';
  if (book.canBorrow) return isNewBook(book) ? 'new' : 'available';
  return 'borrowed';
}

function coverImageStyle(book, index = 0) {
  if (book?.coverImageUrl) {
    return {
      background: 'linear-gradient(135deg, rgba(6, 17, 29, 0.85), rgba(12, 24, 44, 0.95))'
    };
  }

  return coverGradient(book, index);
}

function handleCoverImageError(book) {
  if (!book) return;
  book.coverImageUrl = '';
}

function handleBookFormCoverError() {
  bookForm.coverImageUrl = '';
}

function readerStatusLabel(reader) {
  if (!reader) return 'Không rõ';
  if (reader.status !== 'Active') return reader.status || 'Không hoạt động';
  if (new Date(reader.expiredAtUtc).getTime() <= Date.now()) return 'Hết hạn';
  return 'Hoạt động';
}

function readerStatusTone(reader) {
  if (!reader) return 'neutral';
  if (reader.status !== 'Active') return 'danger';
  if (new Date(reader.expiredAtUtc).getTime() <= Date.now()) return 'danger';
  return 'success';
}

function transactionTone(row) {
  if (isOverdueBorrowing(row)) return 'danger';
  if (row?.status === 'Returned') return 'success';
  return 'neutral';
}

function outstandingFine(row) {
  return Number(row?.outstandingFine || 0);
}

function formatMoney(value) {
  return currencyFormatter.format(value ?? 0);
}

function formatDate(value) {
  if (!value) return 'Chưa có';
  return dateFormatter.format(new Date(value));
}

function statusText(status) {
  const map = {
    Borrowed: 'Đang mượn',
    Overdue: 'Quá hạn',
    Returned: 'Đã trả'
  };
  return map[status] || status || 'Không rõ';
}

function coverGradient(book, index = 0) {
  const palette = [
    'linear-gradient(135deg, #7ce0ff 0%, #2546ff 100%)',
    'linear-gradient(135deg, #ffbd59 0%, #ff6a3d 100%)',
    'linear-gradient(135deg, #4fe4a2 0%, #16a34a 100%)',
    'linear-gradient(135deg, #8b7dff 0%, #d946ef 100%)'
  ];

  return {
    background: palette[(index + (book.title?.length || 0)) % palette.length]
  };
}

function setMessage(type, text) {
  errorMessage.value = '';
  successMessage.value = '';
  if (type === 'error') {
    errorMessage.value = text;
    return;
  }
  successMessage.value = text;
}

function clearContext() {
  dashboard.value = null;
  readers.value = [];
  users.value = [];
  recentTransactions.value = [];
  readerBorrowings.value = [];
  fineSummary.value = null;
  borrowingPolicy.value = null;
  selectedBook.value = null;
  borrowDialog.open = false;
  borrowDialog.book = null;
  setBorrowDialogDays(borrowDays.value);
}

async function loadBooks() {
  loadingBooks.value = true;
  try {
    const endpoint = searchText.value.trim()
      ? `/api/books/search?keyword=${encodeURIComponent(searchText.value.trim())}`
      : '/api/books';
    const result = await apiRequest(endpoint);
    books.value = normalizeList(result);
    if (selectedBook.value) {
      selectedBook.value = books.value.find((book) => book.id === selectedBook.value.id) || selectedBook.value;
    }
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    loadingBooks.value = false;
  }
}

async function loadBookCategories() {
  try {
    const result = await apiRequest('/api/book-categories');
    bookCategories.value = normalizeCategoryList(result);
  } catch {
    const fallback = await apiRequest('/api/books/categories');
    bookCategories.value = normalizeCategoryList(fallback);
  } finally {
    ensureBookFormCategory();
  }
}

async function loadRecentTransactions() {
  if (!auth.isAuthenticated) {
    recentTransactions.value = [];
    return;
  }

  try {
    const endpoint = isStaff.value
      ? '/api/circulations'
      : isReader.value
        ? `/api/circulations/reader/${auth.user.userId}`
        : null;

    if (!endpoint) {
      recentTransactions.value = [];
      return;
    }

    const result = await apiRequest(endpoint, {
      headers: { Authorization: `Bearer ${auth.token}` }
    });
    recentTransactions.value = normalizeList(result);
  } catch (error) {
    recentTransactions.value = [];
  }
}

async function loadStaffData() {
  if (!auth.isAuthenticated || !isStaff.value) {
    dashboard.value = null;
    readers.value = [];
    users.value = [];
    borrowingPolicy.value = null;
    return;
  }

  loadingPanel.value = true;
  try {
    const [dashboardResult, readersResult, policyResult, usersResult] = await Promise.all([
      apiRequest('/api/reports/dashboard', {
        headers: { Authorization: `Bearer ${auth.token}` }
      }),
      apiRequest('/api/readers', {
        headers: { Authorization: `Bearer ${auth.token}` }
      }),
      apiRequest('/api/circulation-rules', {
        headers: { Authorization: `Bearer ${auth.token}` }
      }),
      auth.role === 'Admin'
        ? apiRequest('/api/users', {
            headers: { Authorization: `Bearer ${auth.token}` }
          })
        : Promise.resolve([])
    ]);

    dashboard.value = dashboardResult;
    readers.value = normalizeList(readersResult);
    borrowingPolicy.value = policyResult;
    users.value = normalizeList(usersResult);
    resetPolicyForm(policyResult);
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    loadingPanel.value = false;
  }
}

async function loadReaderData() {
  if (!auth.isAuthenticated || !isReader.value) {
    readerBorrowings.value = [];
    fineSummary.value = null;
    return;
  }

  loadingPanel.value = true;
  try {
    const [borrowingsResult, fineResult] = await Promise.all([
      apiRequest(`/api/circulations/reader/${auth.user.userId}`, {
        headers: { Authorization: `Bearer ${auth.token}` }
      }),
      apiRequest(`/api/circulations/fines?readerId=${auth.user.userId}`, {
        headers: { Authorization: `Bearer ${auth.token}` }
      })
    ]);

    readerBorrowings.value = normalizeList(borrowingsResult);
    fineSummary.value = fineResult;
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    loadingPanel.value = false;
  }
}

async function refreshAuthorizedData() {
  if (!auth.isAuthenticated) {
    clearContext();
    return;
  }

  if (isStaff.value) {
    await loadStaffData();
  } else if (isReader.value) {
    await loadReaderData();
  }

  await loadRecentTransactions();
}

async function submitAuth() {
  try {
    if (loginMode.value === 'login') {
      await auth.login(loginForm.email, loginForm.password);
      setMessage('success', 'Đăng nhập thành công.');
    } else {
      await auth.register(loginForm.fullName, loginForm.email, loginForm.password);
      setMessage('success', 'Đăng ký thành công.');
    }

    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  }
}

function openAddBook() {
  showBookForm.value = true;
  resetBookForm();
  resetExternalSearch();
}

function editBook(book) {
  showBookForm.value = true;
  resetBookForm(book);
}

function showBookDetails(book) {
  selectedBook.value = book;
}

function closeBookDetails() {
  selectedBook.value = null;
}

function cancelBookEdit() {
  resetBookForm();
  resetExternalSearch();
  showBookForm.value = false;
}

async function submitBookForm() {
  if (!isStaff.value) {
    setMessage('error', 'Chỉ Admin hoặc Librarian mới được quản lý sách.');
    return;
  }

  busyAction.value = 'book-form';
  try {
    if (!bookForm.category) {
      setMessage('error', 'Vui lòng chọn thể loại sách.');
      return;
    }

    const payload = {
      isbn: bookForm.isbn.trim(),
      title: bookForm.title.trim(),
      author: bookForm.author.trim(),
      publisher: bookForm.publisher.trim(),
      publishedYear: Number(bookForm.publishedYear),
      category: bookForm.category.trim(),
      totalCopies: Math.max(1, Number(bookForm.totalCopies)),
      minimumCopies: Math.max(0, Math.min(Number(bookForm.minimumCopies), Number(bookForm.totalCopies))),
      coverImageUrl: bookForm.coverImageUrl.trim() || null,
      description: bookForm.description.trim() || null,
      content: bookForm.content.trim() || null
    };

    const endpoint = bookForm.id ? `/api/books/${bookForm.id}` : '/api/books';
    const method = bookForm.id ? 'PUT' : 'POST';

    await apiRequest(endpoint, {
      method,
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify(payload)
    });

    setMessage('success', bookForm.id ? 'Đã cập nhật sách.' : 'Đã thêm sách mới.');
    cancelBookEdit();
    await loadBooks();
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function submitPolicyForm() {
  if (auth.role !== 'Admin') {
    setMessage('error', 'Chỉ Admin mới được cập nhật quy tắc mượn trả.');
    return;
  }

  busyAction.value = 'policy-form';
  try {
    const payload = {
      maxActiveBorrowingsPerReader: Math.max(1, Number(policyForm.maxActiveBorrowingsPerReader)),
      defaultBorrowDays: Math.max(1, Number(policyForm.defaultBorrowDays)),
      maxRenewalDays: Math.max(1, Number(policyForm.maxRenewalDays)),
      finePerOverdueDay: Math.max(0, Number(policyForm.finePerOverdueDay)),
      allowReaderSelfCheckout: Boolean(policyForm.allowReaderSelfCheckout)
    };

    const result = await apiRequest('/api/circulation-rules', {
      method: 'PUT',
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify(payload)
    });

    borrowingPolicy.value = result;
    resetPolicyForm(result);
    setMessage('success', 'Đã cập nhật quy tắc mượn trả.');
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function submitCategoryForm() {
  if (auth.role !== 'Admin') {
    setMessage('error', 'Chỉ Admin mới được thêm thể loại sách.');
    return;
  }

  const name = categoryForm.name.trim();
  if (!name) {
    setMessage('error', 'Vui lòng nhập tên thể loại.');
    return;
  }

  busyAction.value = 'category-form';
  try {
    const result = await apiRequest('/api/book-categories', {
      method: 'POST',
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify({
        name,
        description: categoryForm.description.trim() || null
      })
    });

    setMessage('success', `Đã thêm thể loại "${result.name}".`);
    resetCategoryForm();
    await loadBookCategories();
    bookForm.category = result.name;
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function searchExternalBooks() {
  if (!isStaff.value) return;

  const query = externalSearch.query.trim();
  if (!query) {
    setMessage('error', 'Nhập tên sách, tác giả hoặc ISBN để tìm online.');
    return;
  }

  externalSearch.loading = true;
  externalSearch.results = [];
  try {
    const result = await apiRequest(`/api/books/import-search?query=${encodeURIComponent(query)}&limit=6`, {
      headers: { Authorization: `Bearer ${auth.token}` }
    });
    externalSearch.results = normalizeList(result);
    if (!externalSearch.results.length) {
      setMessage('error', 'Không tìm thấy sách phù hợp từ nguồn online.');
      return;
    }

    setMessage('success', `Tìm thấy ${externalSearch.results.length} kết quả online.`);
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    externalSearch.loading = false;
  }
}

function handleCoverFileChange(event) {
  const file = event?.target?.files?.[0];
  if (!file) return;

  if (!file.type.startsWith('image/')) {
    setMessage('error', 'Vui lòng chọn một file ảnh hợp lệ.');
    event.target.value = '';
    return;
  }

  const maxBytes = 6 * 1024 * 1024;
  if (file.size > maxBytes) {
    setMessage('error', 'Ảnh quá lớn, vui lòng chọn file nhỏ hơn 6MB.');
    event.target.value = '';
    return;
  }

  const reader = new FileReader();
  reader.onload = () => {
    bookForm.coverImageUrl = typeof reader.result === 'string' ? reader.result : '';
    setMessage('success', 'Đã nạp ảnh bìa trực tiếp vào form.');
  };
  reader.onerror = () => {
    setMessage('error', 'Không thể đọc file ảnh.');
  };
  reader.readAsDataURL(file);
}

async function archiveBook(book) {
  if (!isStaff.value) return;
  if (!window.confirm(`Lưu trữ sách "${book.title}"?`)) return;

  busyAction.value = `archive:${book.id}`;
  try {
    await apiRequest(`/api/books/${book.id}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${auth.token}` }
    });

    setMessage('success', `Đã lưu trữ sách "${book.title}".`);
    await loadBooks();
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function restoreBook(book) {
  if (!isStaff.value) return;
  if (!window.confirm(`Khôi phục sách "${book.title}"?`)) return;

  busyAction.value = `restore:${book.id}`;
  try {
    await apiRequest(`/api/books/${book.id}/restore`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${auth.token}` }
    });

    setMessage('success', `Đã khôi phục sách "${book.title}".`);
    await loadBooks();
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function deleteBookPermanently(book) {
  if (!isAdmin.value) return;
  if (!window.confirm(`Xóa vĩnh viễn sách "${book.title}"? Hành động này không thể hoàn tác.`)) return;

  busyAction.value = `delete-book:${book.id}`;
  try {
    await apiRequest(`/api/books/${book.id}/permanent`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${auth.token}` }
    });

    if (selectedBook.value?.id === book.id) {
      selectedBook.value = null;
    }

    setMessage('success', `Đã xóa vĩnh viễn sách "${book.title}".`);
    await loadBooks();
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

function borrowBook(book) {
  if (!auth.isAuthenticated || !isReader.value) {
    setMessage('error', 'Bạn cần đăng nhập tài khoản Reader để mượn sách.');
    return;
  }

  if (!book?.canBorrow) {
    setMessage('error', 'Sách hiện không còn bản có thể mượn.');
    return;
  }

  borrowDialog.book = book;
  setBorrowDialogDays(borrowDays.value || 14);
  borrowDialog.open = true;
}

function closeBorrowDialog(force = false) {
  if (!force && borrowDialog.book && busyAction.value === `borrow:${borrowDialog.book.id}`) return;
  borrowDialog.open = false;
  borrowDialog.book = null;
  setBorrowDialogDays(borrowDays.value || 14);
}

async function confirmBorrowBook() {
  const book = borrowDialog.book;
  if (!book) return;

  const dueDateDays = daysUntilDateInput(borrowDialog.dueDate);
  const selectedDays = Number.isFinite(dueDateDays)
    ? dueDateDays
    : Number(borrowDialog.days || borrowDays.value || 14);
  if (!Number.isFinite(selectedDays) || selectedDays <= 0 || selectedDays > 365) {
    setMessage('error', 'Thời hạn mượn phải nằm trong khoảng 1 đến 365 ngày.');
    return;
  }

  busyAction.value = `borrow:${book.id}`;
  try {
    await apiRequest('/api/circulations', {
      method: 'POST',
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify({
        readerId: auth.user.userId,
        bookId: book.id,
        borrowDays: selectedDays
      })
    });

    borrowDays.value = selectedDays;
    setMessage('success', `Đã tạo phiếu mượn cho "${book.title}", hạn trả ${formatDate(borrowDialogDueDate.value)}.`);
    closeBorrowDialog(true);
    await loadBooks();
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function returnBook(record) {
  if (!auth.isAuthenticated) return;

  busyAction.value = `return:${record.id}`;
  try {
    await apiRequest(`/api/circulations/${record.id}/return`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify({ returnedAtUtc: new Date().toISOString() })
    });

    setMessage('success', `Đã trả sách "${record.bookTitle}".`);
    await loadBooks();
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function payFine(record) {
  if (!isStaff.value || outstandingFine(record) <= 0) return;

  const amountText = window.prompt(
    `Nhập số tiền thu cho "${record.bookTitle}"`,
    String(outstandingFine(record))
  );
  if (amountText === null) return;

  const amount = Number(amountText);
  if (!Number.isFinite(amount) || amount <= 0) {
    setMessage('error', 'Số tiền thu không hợp lệ.');
    return;
  }

  busyAction.value = `pay-fine:${record.id}`;
  try {
    await apiRequest(`/api/circulations/${record.id}/fine-payment`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify({ amount })
    });

    setMessage('success', `Đã ghi nhận thu phạt cho "${record.bookTitle}".`);
    await loadRecentTransactions();
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function toggleUserStatus(user) {
  if (auth.role !== 'Admin') return;

  busyAction.value = `user-status:${user.userId}`;
  try {
    await apiRequest(`/api/users/${user.userId}/status`, {
      method: 'PUT',
      headers: { Authorization: `Bearer ${auth.token}` },
      body: JSON.stringify({ isActive: !user.isActive })
    });

    setMessage('success', user.isActive ? 'Đã khóa tài khoản.' : 'Đã mở tài khoản.');
    await loadStaffData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

async function deleteReader(reader) {
  if (!isStaff.value) return;
  if (!window.confirm(`Xóa độc giả "${reader.fullName}"?`)) return;

  busyAction.value = `delete-reader:${reader.userId}`;
  try {
    await apiRequest(`/api/readers/${reader.userId}`, {
      method: 'DELETE',
      headers: { Authorization: `Bearer ${auth.token}` }
    });

    setMessage('success', `Đã xóa độc giả "${reader.fullName}".`);
    await refreshAuthorizedData();
  } catch (error) {
    setMessage('error', error.message);
  } finally {
    busyAction.value = '';
  }
}

function logout() {
  auth.logout();
  clearContext();
  cancelBookEdit();
  setMessage('success', 'Đã đăng xuất.');
}

function jumpTo(id) {
  document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
}

watch(searchText, (value) => {
  window.clearTimeout(searchTimer.value);
  searchTimer.value = window.setTimeout(() => {
    loadBooks(value);
  }, 250);
});

watch(
  () => auth.token,
  async () => {
    if (auth.isAuthenticated) {
      await refreshAuthorizedData();
      return;
    }

    clearContext();
  }
);

onMounted(async () => {
  await Promise.all([loadBookCategories(), loadBooks()]);
  await refreshAuthorizedData();
});
</script>

<template>
  <div class="dashboard-shell">
    <aside class="sidebar slide-in-left">
      <div class="brand">
        <div class="brand-mark">C</div>
        <div>
          <p>Circulation</p>
          <strong>Library Suite</strong>
        </div>
      </div>

      <nav class="menu">
        <button type="button" class="menu-item active" @click="jumpTo('topbar')">Tổng quan</button>
        <button type="button" class="menu-item" @click="jumpTo('books')">Sách mới nhập</button>
        <button type="button" class="menu-item" @click="jumpTo('transactions')">Giao dịch</button>
        <button type="button" class="menu-item" @click="jumpTo('reports')">Báo cáo</button>
      </nav>

      <div class="sidebar-badge badge-pop">
        <span>Thông báo</span>
        <strong>{{ staffNotifications }}</strong>
      </div>

      <div class="sidebar-card">
        <div class="account-head">
          <div class="avatar">{{ accountInitial }}</div>
          <div>
            <p class="muted">Tài khoản</p>
            <strong>{{ auth.isAuthenticated ? auth.user?.fullName : 'Chưa đăng nhập' }}</strong>
            <span>{{ roleLabel }}</span>
          </div>
        </div>

        <div v-if="auth.isAuthenticated" class="account-meta">
          <div>
            <span>Email</span>
            <strong>{{ auth.user?.email }}</strong>
          </div>
          <div>
            <span>Vai trò</span>
            <strong>{{ roleLabel }}</strong>
          </div>
        </div>

        <div v-if="!auth.isAuthenticated" class="auth-switch">
          <button :class="{ active: loginMode === 'login' }" type="button" @click="loginMode = 'login'">Đăng nhập</button>
          <button :class="{ active: loginMode === 'register' }" type="button" @click="loginMode = 'register'">Đăng ký</button>
        </div>

        <form v-if="!auth.isAuthenticated" class="auth-form" @submit.prevent="submitAuth">
          <label v-if="loginMode === 'register'">
            <span>Họ và tên</span>
            <input v-model="loginForm.fullName" type="text" placeholder="Nguyễn Văn A" />
          </label>
          <label>
            <span>Email</span>
            <input v-model="loginForm.email" type="email" placeholder="admin@library.local" />
          </label>
          <label>
            <span>Mật khẩu</span>
            <input v-model="loginForm.password" type="password" placeholder="••••••••" />
          </label>
          <button class="primary-button" type="submit">
            {{ loginMode === 'login' ? 'Đăng nhập' : 'Tạo tài khoản' }}
          </button>
        </form>

        <button v-else class="ghost-button full-width" type="button" @click="logout">Đăng xuất</button>
      </div>
    </aside>

    <div class="content">
      <header id="topbar" class="topbar fade-up">
        <div class="topbar-title">
          <p class="eyebrow">Dashboard</p>
          <h1>Quản lý thư viện theo thời gian thực</h1>
        </div>

        <div class="topbar-actions">
          <label class="search-box">
            <span>Search</span>
            <input v-model="searchText" type="search" placeholder="Tìm theo tên, ISBN, tác giả, NXB, mô tả..." />
          </label>

          <div class="topbar-action-row">
            <button v-if="isStaff" class="primary-button add-book-btn" type="button" @click="openAddBook">
              + Thêm sách
            </button>

            <button class="notif-btn badge-pop" type="button" @click="jumpTo('transactions')">
              <span>Chuông</span>
              <strong>{{ staffNotifications }}</strong>
            </button>
          </div>
        </div>
      </header>

      <section class="stats-grid fade-up" style="--delay: 90ms">
        <article class="stat-card">
          <div class="stat-top">
            <span>Tổng sách</span>
            <em class="trend up">+12%</em>
          </div>
          <strong>{{ totalBooks }}</strong>
          <p>Danh mục đang có trong hệ thống.</p>
        </article>
        <article class="stat-card">
          <div class="stat-top">
            <span>Độc giả</span>
            <em class="trend up">+8%</em>
          </div>
          <strong>{{ totalReaders }}</strong>
          <p>Người dùng có hồ sơ Reader hoạt động.</p>
        </article>
        <article class="stat-card">
          <div class="stat-top">
            <span>Đang mượn</span>
            <em class="trend down">-3%</em>
          </div>
          <strong>{{ activeBorrowings }}</strong>
          <p>Phiếu còn hiệu lực và chưa trả.</p>
        </article>
        <article class="stat-card">
          <div class="stat-top">
            <span>Quá hạn</span>
            <em class="trend down">-1%</em>
          </div>
          <strong>{{ overdueBorrowings }}</strong>
          <p>Cần ưu tiên xử lý trong ngày.</p>
        </article>
      </section>

      <section id="books" class="section-block fade-up" style="--delay: 180ms">
        <div class="section-head">
          <div>
            <p class="eyebrow">Catalog mới nhập</p>
            <h2>Sách mới nhập</h2>
          </div>
          <div class="pill-row">
            <button
              v-for="pill in categories"
              :key="pill.key"
              type="button"
              class="filter-pill"
              :class="{ active: selectedCategory === pill.key }"
              @click="selectedCategory = pill.key"
            >
              {{ pill.label }}
            </button>
          </div>
        </div>

        <div v-if="isReader" class="borrow-settings scale-in">
          <div>
            <p class="eyebrow">Thiết lập mượn</p>
            <h3>Chọn hạn trả trước khi mượn</h3>
            <p>
              Hạn mượn đang chọn là {{ borrowDays }} ngày.
              Dự kiến trả vào {{ formatDate(borrowDeadlinePreview) }}.
            </p>
          </div>

          <label class="borrow-days-picker">
            <span>Thời hạn mượn</span>
            <select v-model.number="borrowDays">
              <option v-for="day in borrowDaysOptions" :key="day" :value="day">{{ day }} ngày</option>
            </select>
          </label>
        </div>

        <div v-if="showBookForm && isStaff" class="book-form-panel scale-in">
          <div class="panel-head">
            <div>
              <p class="eyebrow">Quản trị sách</p>
              <h3>{{ selectedBookFormTitle }}</h3>
            </div>
            <button class="ghost-button" type="button" @click="cancelBookEdit">Đóng</button>
          </div>

          <div v-if="auth.role === 'Admin'" class="category-manager">
            <div class="category-manager-head">
              <div>
                <p class="eyebrow">Thể loại</p>
                <h3>Danh mục thể loại</h3>
              </div>
              <span class="row-badge neutral">{{ categoryOptions.length }} mục</span>
            </div>

            <form class="category-form" @submit.prevent="submitCategoryForm">
              <label>
                <span>Tên thể loại</span>
                <input v-model="categoryForm.name" type="text" placeholder="Ví dụ: Khoa học dữ liệu" />
              </label>
              <label>
                <span>Ghi chú</span>
                <input v-model="categoryForm.description" type="text" placeholder="Tùy chọn" />
              </label>
              <button class="primary-button" type="submit" :disabled="busyAction === 'category-form'">
                {{ busyAction === 'category-form' ? 'Đang thêm...' : 'Thêm thể loại' }}
              </button>
            </form>

            <div class="category-chip-row">
              <button
                v-for="category in categoryOptions"
                :key="category.id"
                type="button"
                class="category-chip"
                :class="{ active: bookForm.category === category.name }"
                @click="bookForm.category = category.name"
              >
                {{ category.name }}
              </button>
            </div>
          </div>

          <div class="online-import-panel">
            <div class="category-manager-head">
              <div>
                <p class="eyebrow">Dữ liệu online</p>
                <h3>Lấy thông tin sách từ Open Library</h3>
              </div>
              <span class="row-badge neutral">Metadata</span>
            </div>

            <form class="online-import-form" @submit.prevent="searchExternalBooks">
              <label>
                <span>Tên sách, tác giả hoặc ISBN</span>
                <input v-model="externalSearch.query" type="search" placeholder="Ví dụ: Clean Code, 9780132350884..." />
              </label>
              <button class="primary-button" type="submit" :disabled="externalSearch.loading">
                {{ externalSearch.loading ? 'Đang tìm...' : 'Tìm online' }}
              </button>
            </form>

            <div v-if="externalSearch.results.length" class="external-result-list">
              <article v-for="candidate in externalSearch.results" :key="candidate.sourceId" class="external-result">
                <div class="external-cover" :style="candidate.coverImageUrl ? {} : coverGradient(candidate, 0)">
                  <img
                    v-if="candidate.coverImageUrl"
                    :src="candidate.coverImageUrl"
                    :alt="candidate.title"
                    @error="handleCoverImageError(candidate)"
                  />
                </div>
                <div>
                  <strong>{{ candidate.title }}</strong>
                  <p>{{ candidate.author }} · {{ candidate.publishedYear || 'Không rõ năm' }}</p>
                  <p>{{ candidate.publisher }}</p>
                  <p v-if="candidate.suggestedCategory">Gợi ý: {{ candidate.suggestedCategory }}</p>
                </div>
                <button class="ghost-button small" type="button" @click="applyExternalBook(candidate)">
                  Áp dụng
                </button>
              </article>
            </div>
          </div>

          <form class="book-form" @submit.prevent="submitBookForm">
            <div class="form-grid">
              <label>
                <span>ISBN</span>
                <input v-model="bookForm.isbn" type="text" placeholder="978-..." />
              </label>
              <label>
                <span>Tiêu đề</span>
                <input v-model="bookForm.title" type="text" placeholder="Tên sách" />
              </label>
              <label>
                <span>Tác giả</span>
                <input v-model="bookForm.author" type="text" placeholder="Tác giả" />
              </label>
              <label>
                <span>Nhà xuất bản</span>
                <input v-model="bookForm.publisher" type="text" placeholder="NXB" />
              </label>
              <label>
                <span>Năm</span>
                <input v-model="bookForm.publishedYear" type="number" min="1900" max="2100" />
              </label>
              <label>
                <span>Thể loại</span>
                <select v-model="bookForm.category">
                  <option value="" disabled>Chọn thể loại</option>
                  <option v-for="category in categoryOptions" :key="category.id" :value="category.name">
                    {{ category.name }}
                  </option>
                </select>
              </label>
              <label>
                <span>Tổng bản</span>
                <input v-model="bookForm.totalCopies" type="number" min="1" />
              </label>
              <label>
                <span>Ngưỡng tối thiểu</span>
                <input v-model="bookForm.minimumCopies" type="number" min="0" />
              </label>
            </div>

            <div class="form-grid form-grid-wide">
              <label>
                <span>Ảnh bìa</span>
                <input v-model="bookForm.coverImageUrl" type="url" placeholder="https://..." />
                <input type="file" accept="image/*" @change="handleCoverFileChange" />
              </label>
              <label>
                <span>Mô tả</span>
                <textarea v-model="bookForm.description" rows="3" placeholder="Mô tả ngắn..."></textarea>
              </label>
            </div>

            <label class="content-field">
              <span>Nội dung giới thiệu / ghi chú</span>
              <textarea
                v-model="bookForm.content"
                rows="6"
                placeholder="Tóm tắt, ghi chú thủ thư, dữ liệu preview hợp lệ từ nguồn online..."
              ></textarea>
            </label>

            <div class="cover-preview-row">
              <div class="cover-preview">
                <img
                  v-if="bookForm.coverImageUrl"
                  :src="bookForm.coverImageUrl"
                  :alt="bookForm.title || 'Ảnh bìa sách'"
                  @error="handleBookFormCoverError"
                />
                <div v-else class="cover-placeholder" :style="coverGradient(bookForm, 0)">
                  <strong>Chưa có ảnh bìa</strong>
                  <span>URL hoặc file ảnh sẽ hiển thị ở đây</span>
                </div>
              </div>
              <p class="muted">
                Bạn có thể dán URL ảnh bìa hoặc tải ảnh trực tiếp lên. Ảnh được kiểm tra ngay trong form và lưu cùng sách.
              </p>
            </div>

            <div class="form-actions">
              <button class="primary-button" type="submit" :disabled="busyAction === 'book-form'">
                {{ busyAction === 'book-form' ? 'Đang lưu...' : (bookForm.id ? 'Cập nhật' : 'Thêm sách') }}
              </button>
              <p class="muted">Dữ liệu được lưu qua API Gateway và đồng bộ xuống Catalog Service.</p>
            </div>
          </form>
        </div>

        <div v-if="loadingBooks" class="empty-state">Đang tải catalog...</div>
        <div v-else-if="!visibleBooks.length" class="empty-state">Không tìm thấy sách phù hợp.</div>

        <div v-else class="books-grid">
          <article
            v-for="(book, index) in visibleBooks"
            :key="book.id"
            class="book-card scale-in"
            :style="{ '--delay': `${index * 80}ms` }"
          >
            <button
              type="button"
              class="book-cover"
              :style="coverImageStyle(book, index)"
              @click="showBookDetails(book)"
            >
              <img
                v-if="book.coverImageUrl"
                class="book-cover-image"
                :src="book.coverImageUrl"
                :alt="book.title"
                @error="handleCoverImageError(book)"
              />
              <div v-else class="book-cover-fallback"></div>
              <div class="book-cover-overlay">
                <span>{{ bookStatusLabel(book) }}</span>
                <strong>{{ book.category }}</strong>
                <small>Ấn để xem giới thiệu</small>
              </div>
            </button>

            <div class="book-head">
              <div>
                <h3>{{ book.title }}</h3>
                <p>{{ book.author }}</p>
              </div>
              <span class="book-status" :class="bookStatusTone(book)">{{ bookStatusLabel(book) }}</span>
            </div>

            <div class="book-meta">
              <span>{{ book.publisher }}</span>
              <span>{{ book.publishedYear }}</span>
            </div>

            <div class="book-stock">
              <div class="stock-bar">
                <span :style="{ width: `${(book.availableCopies / Math.max(book.totalCopies, 1)) * 100}%` }"></span>
              </div>
              <div class="stock-line">
                <span>{{ book.availableCopies }}/{{ book.totalCopies }} bản</span>
                <span>Ngưỡng {{ book.minimumCopies }}</span>
              </div>
            </div>

            <p v-if="book.description" class="book-desc">{{ book.description }}</p>

            <div class="book-actions">
              <button
                v-if="isReader"
                class="primary-button small"
                type="button"
                :disabled="busyAction === `borrow:${book.id}` || !book.canBorrow"
                @click="borrowBook(book)"
              >
                {{ busyAction === `borrow:${book.id}` ? 'Đang xử lý...' : 'Mượn sách' }}
              </button>

              <button class="ghost-button small" type="button" @click="showBookDetails(book)">Chi tiết</button>

              <template v-if="isStaff">
                <button class="ghost-button small" type="button" @click="editBook(book)">Sửa</button>
                <button
                  class="ghost-button small danger"
                  type="button"
                  :disabled="busyAction === `archive:${book.id}` || busyAction === `restore:${book.id}`"
                  @click="book.isArchived ? restoreBook(book) : archiveBook(book)"
                >
                  {{ book.isArchived ? 'Khôi phục' : 'Lưu trữ' }}
                </button>
                <button
                  v-if="isAdmin"
                  class="ghost-button small danger"
                  type="button"
                  :disabled="busyAction === `delete-book:${book.id}`"
                  @click="deleteBookPermanently(book)"
                >
                  {{ busyAction === `delete-book:${book.id}` ? 'Đang xóa...' : 'Xóa' }}
                </button>
              </template>
            </div>
          </article>
        </div>
      </section>

      <section id="transactions" class="section-block fade-up" style="--delay: 270ms">
        <div class="section-head">
          <div>
            <p class="eyebrow">Hoạt động gần đây</p>
            <h2>Bảng giao dịch mượn trả</h2>
          </div>
          <p class="section-note">Các dòng quá hạn được highlight màu đỏ.</p>
        </div>

        <div class="table-card">
          <table class="transaction-table">
            <thead>
              <tr>
                <th>Sách</th>
                <th>Độc giả</th>
                <th>Ngày mượn</th>
                <th>Hạn trả</th>
                <th>Trạng thái</th>
                <th>Phạt</th>
                <th>Công nợ</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="row in recentBorrowingRows"
                :key="row.id"
                :class="[
                  `tone-${transactionTone(row)}`,
                  { overdue: isOverdueBorrowing(row) }
                ]"
              >
                <td>
                  <strong>{{ row.bookTitle }}</strong>
                  <p>{{ row.bookId }}</p>
                </td>
                <td>{{ row.readerId }}</td>
                <td>{{ formatDate(row.borrowedAtUtc) }}</td>
                <td>{{ formatDate(row.dueAtUtc) }}</td>
                <td>
                  <span class="row-badge" :class="transactionTone(row)">{{ statusText(row.status) }}</span>
                  <p v-if="isOverdueBorrowing(row)" class="overdue-note">Quá hạn {{ overdueDays(row) }} ngày</p>
                </td>
                <td>{{ formatMoney(row.fineAmount) }}</td>
                <td>{{ formatMoney(outstandingFine(row)) }}</td>
                <td>
                  <button
                    v-if="isStaff && outstandingFine(row) > 0"
                    class="ghost-button small"
                    type="button"
                    :disabled="busyAction === `pay-fine:${row.id}`"
                    @click="payFine(row)"
                  >
                    Thu phạt
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section id="reports" class="section-block fade-up" style="--delay: 360ms">
        <div class="section-head">
          <div>
            <p class="eyebrow">Báo cáo</p>
            <h2>Thông tin theo vai trò</h2>
          </div>
        </div>

        <div v-if="isStaff && borrowingPolicy" class="info-card policy-card">
          <div class="panel-head">
            <div>
              <p class="eyebrow">Quy tắc mượn trả</p>
              <h3>Cấu hình nghiệp vụ Circulation</h3>
            </div>
            <span class="row-badge neutral">Cập nhật: {{ formatDate(borrowingPolicy.updatedAtUtc) }}</span>
          </div>

          <form class="policy-form" @submit.prevent="submitPolicyForm">
            <label>
              <span>Số phiếu tối đa / độc giả</span>
              <input v-model.number="policyForm.maxActiveBorrowingsPerReader" type="number" min="1" max="50" :disabled="auth.role !== 'Admin'" />
            </label>
            <label>
              <span>Ngày mượn mặc định</span>
              <input v-model.number="policyForm.defaultBorrowDays" type="number" min="1" max="365" :disabled="auth.role !== 'Admin'" />
            </label>
            <label>
              <span>Gia hạn tối đa</span>
              <input v-model.number="policyForm.maxRenewalDays" type="number" min="1" max="365" :disabled="auth.role !== 'Admin'" />
            </label>
            <label>
              <span>Phạt / ngày</span>
              <input v-model.number="policyForm.finePerOverdueDay" type="number" min="0" step="1000" :disabled="auth.role !== 'Admin'" />
            </label>
            <label class="toggle-row">
              <input v-model="policyForm.allowReaderSelfCheckout" type="checkbox" :disabled="auth.role !== 'Admin'" />
              <span>Cho phép Reader tự tạo phiếu mượn</span>
            </label>
            <button v-if="auth.role === 'Admin'" class="primary-button" type="submit" :disabled="busyAction === 'policy-form'">
              {{ busyAction === 'policy-form' ? 'Đang lưu...' : 'Lưu quy tắc' }}
            </button>
          </form>
        </div>

        <div class="bottom-grid">
          <div class="info-card">
            <h3>Độc giả gần đây</h3>
            <div class="info-list">
              <article v-for="reader in readers.slice(0, 5)" :key="reader.userId" class="info-row">
                <div>
                  <strong>{{ reader.fullName }}</strong>
                  <p>{{ reader.email }}</p>
                  <p>Mã thẻ: {{ reader.libraryCardNumber }} · Hết hạn: {{ formatDate(reader.expiredAtUtc) }}</p>
                </div>
                <div class="reader-row-actions">
                  <span class="row-badge" :class="readerStatusTone(reader)">{{ readerStatusLabel(reader) }}</span>
                  <span class="row-badge neutral">{{ reader.role }}</span>
                  <button
                    v-if="isStaff"
                    class="ghost-button small danger"
                    type="button"
                    :disabled="busyAction === `delete-reader:${reader.userId}`"
                    @click="deleteReader(reader)"
                  >
                    Xóa
                  </button>
                </div>
              </article>
            </div>
          </div>

          <div class="info-card">
            <h3>Quá hạn cần xử lý</h3>
            <div class="info-list">
              <article v-for="row in recentBorrowingRows.filter(isOverdueBorrowing).slice(0, 5)" :key="row.id" class="info-row danger-row">
                <div>
                  <strong>{{ row.bookTitle }}</strong>
                  <p>Hạn: {{ formatDate(row.dueAtUtc) }} · Quá hạn {{ overdueDays(row) }} ngày</p>
                </div>
                <button class="ghost-button small" type="button" :disabled="busyAction === `return:${row.id}`" @click="returnBook(row)">
                  Trả
                </button>
              </article>
              <p v-if="!recentBorrowingRows.filter(isOverdueBorrowing).length" class="muted">Không có phiếu quá hạn.</p>
            </div>
          </div>

          <div v-if="auth.role === 'Admin'" class="info-card">
            <h3>Tài khoản hệ thống</h3>
            <div class="info-list">
              <article v-for="user in users.slice(0, 6)" :key="user.userId" class="info-row">
                <div>
                  <strong>{{ user.fullName }}</strong>
                  <p>{{ user.email }}</p>
                  <p>Vai trò: {{ user.role }}</p>
                </div>
                <div class="reader-row-actions">
                  <span class="row-badge" :class="user.isActive ? 'success' : 'danger'">
                    {{ user.isActive ? 'Hoạt động' : 'Đã khóa' }}
                  </span>
                  <button
                    class="ghost-button small"
                    type="button"
                    :disabled="busyAction === `user-status:${user.userId}`"
                    @click="toggleUserStatus(user)"
                  >
                    {{ user.isActive ? 'Khóa' : 'Mở' }}
                  </button>
                </div>
              </article>
              <p v-if="!users.length" class="muted">Chưa tải được danh sách tài khoản.</p>
            </div>
          </div>
        </div>

        <div v-if="isReader" class="reader-summary">
          <div class="summary-card">
            <span>Phiếu đang mượn</span>
            <strong>{{ readerBorrowings.filter(isActiveBorrowing).length }}</strong>
          </div>
          <div class="summary-card">
            <span>Quá hạn</span>
            <strong>{{ readerBorrowings.filter(isOverdueBorrowing).length }}</strong>
          </div>
          <div class="summary-card">
            <span>Công nợ</span>
            <strong>{{ formatMoney(fineSummary?.debtAmount) }}</strong>
          </div>
        </div>

        <div v-if="isReader" class="info-card reader-borrow-card">
          <h3>Phiếu mượn của bạn</h3>
          <div class="info-list">
            <article
              v-for="record in readerBorrowings"
              :key="record.id"
              class="info-row"
              :class="{ 'danger-row': isOverdueBorrowing(record) }"
            >
              <div>
                <strong>{{ record.bookTitle }}</strong>
                <p>{{ statusText(record.status) }} • Hạn trả: {{ formatDate(record.dueAtUtc) }}</p>
                <p v-if="isOverdueBorrowing(record)" class="overdue-note">
                  Quá hạn {{ overdueDays(record) }} ngày · Công nợ đang tính trong tổng phía trên
                </p>
                <p v-else-if="isActiveBorrowing(record)">
                  Còn {{ remainingBorrowDays(record) }} ngày
                </p>
              </div>
              <button
                v-if="isActiveBorrowing(record)"
                class="ghost-button small"
                type="button"
                :disabled="busyAction === `return:${record.id}`"
                @click="returnBook(record)"
              >
                Trả sách
              </button>
            </article>
          </div>
          <p v-if="!readerBorrowings.length" class="muted">Chưa có phiếu mượn nào.</p>
        </div>
      </section>
    </div>

    <div v-if="selectedBook" class="modal-backdrop" @click.self="closeBookDetails">
      <div class="book-detail-modal scale-in">
        <button class="modal-close ghost-button small" type="button" @click="closeBookDetails">Đóng</button>
        <div class="book-detail-grid">
          <div class="detail-cover" :style="coverImageStyle(selectedBook)">
            <img
              v-if="selectedBook.coverImageUrl"
              :src="selectedBook.coverImageUrl"
              :alt="selectedBook.title"
              @error="handleCoverImageError(selectedBook)"
            />
            <div v-else class="cover-placeholder" :style="coverGradient(selectedBook, 0)">
              <strong>{{ selectedBook.category }}</strong>
              <span>{{ bookStatusLabel(selectedBook) }}</span>
            </div>
          </div>

          <div class="detail-content">
            <p class="eyebrow">Giới thiệu sách</p>
            <h3>{{ selectedBook.title }}</h3>
            <p class="detail-description">
              {{ selectedBook.description || 'Chưa có mô tả cho sách này.' }}
            </p>

            <div v-if="selectedBook.content" class="detail-content-box">
              <span>Nội dung</span>
              <p>{{ selectedBook.content }}</p>
            </div>

            <div class="detail-meta">
              <div>
                <span>Tác giả</span>
                <strong>{{ selectedBook.author }}</strong>
              </div>
              <div>
                <span>Nhà xuất bản</span>
                <strong>{{ selectedBook.publisher }}</strong>
              </div>
              <div>
                <span>Năm xuất bản</span>
                <strong>{{ selectedBook.publishedYear }}</strong>
              </div>
              <div>
                <span>ISBN</span>
                <strong>{{ selectedBook.isbn }}</strong>
              </div>
              <div>
                <span>Trạng thái</span>
                <strong>{{ bookStatusLabel(selectedBook) }}</strong>
              </div>
              <div>
                <span>Còn lại</span>
                <strong>{{ selectedBook.availableCopies }}/{{ selectedBook.totalCopies }}</strong>
              </div>
            </div>

            <div class="book-actions">
              <button
                v-if="isReader"
                class="primary-button small"
                type="button"
                :disabled="busyAction === `borrow:${selectedBook.id}` || !selectedBook.canBorrow"
                @click="borrowBook(selectedBook)"
              >
                {{ busyAction === `borrow:${selectedBook.id}` ? 'Đang xử lý...' : 'Mượn sách' }}
              </button>
              <template v-if="isStaff">
                <button class="ghost-button small" type="button" @click="editBook(selectedBook); closeBookDetails()">
                  Sửa
                </button>
                <button
                  class="ghost-button small danger"
                  type="button"
                  :disabled="busyAction === `archive:${selectedBook.id}` || busyAction === `restore:${selectedBook.id}`"
                  @click="selectedBook.isArchived ? restoreBook(selectedBook) : archiveBook(selectedBook)"
                >
                  {{ selectedBook.isArchived ? 'Khôi phục' : 'Lưu trữ' }}
                </button>
                <button
                  v-if="isAdmin"
                  class="ghost-button small danger"
                  type="button"
                  :disabled="busyAction === `delete-book:${selectedBook.id}`"
                  @click="deleteBookPermanently(selectedBook)"
                >
                  {{ busyAction === `delete-book:${selectedBook.id}` ? 'Đang xóa...' : 'Xóa' }}
                </button>
              </template>
              <button class="ghost-button small" type="button" @click="closeBookDetails">Đóng</button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="borrowDialog.open && borrowDialog.book" class="modal-backdrop" @click.self="closeBorrowDialog()">
      <div class="borrow-confirm-modal scale-in">
        <button class="modal-close ghost-button small" type="button" @click="closeBorrowDialog()">Đóng</button>
        <p class="eyebrow">Xác nhận mượn sách</p>
        <h3>{{ borrowDialog.book.title }}</h3>
        <p class="detail-description">
          Chọn ngày trả hoặc nhập số ngày mượn trước khi tạo phiếu.
        </p>

        <div class="borrow-choice-grid">
          <label class="borrow-days-picker">
            <span>Chọn nhanh</span>
            <select :value="borrowDialogQuickDays" @change="setBorrowDialogDays(Number($event.target.value))">
              <option value="" disabled>Tùy chỉnh</option>
              <option v-for="day in borrowDaysOptions" :key="day" :value="String(day)">{{ day }} ngày</option>
            </select>
          </label>
          <label class="borrow-days-picker">
            <span>Số ngày mượn</span>
            <input
              v-model.number="borrowDialog.days"
              type="number"
              min="1"
              max="365"
              @change="setBorrowDialogDays(borrowDialog.days)"
            />
          </label>
          <label class="borrow-days-picker">
            <span>Ngày trả</span>
            <input
              v-model="borrowDialog.dueDate"
              type="date"
              :min="borrowMinDueDate"
              :max="borrowMaxDueDate"
              @change="syncBorrowDialogDaysFromDueDate"
            />
          </label>

          <div class="borrow-summary">
            <span>Ngày mượn</span>
            <strong>{{ formatDate(new Date()) }}</strong>
          </div>
          <div class="borrow-summary">
            <span>Hạn trả dự kiến</span>
            <strong>{{ formatDate(borrowDialogDueDate) }}</strong>
          </div>
          <div class="borrow-summary">
            <span>Số bản còn</span>
            <strong>{{ borrowDialog.book.availableCopies }}/{{ borrowDialog.book.totalCopies }}</strong>
          </div>
        </div>

        <div class="borrow-confirm-actions">
          <button class="ghost-button" type="button" @click="closeBorrowDialog()">Hủy</button>
          <button
            class="primary-button"
            type="button"
            :disabled="busyAction === `borrow:${borrowDialog.book.id}`"
            @click="confirmBorrowBook"
          >
            {{ busyAction === `borrow:${borrowDialog.book.id}` ? 'Đang tạo phiếu...' : 'Xác nhận mượn' }}
          </button>
        </div>
      </div>
    </div>

    <div class="toast-bar">
      <span v-if="errorMessage" class="toast error">{{ errorMessage }}</span>
      <span v-if="successMessage" class="toast success">{{ successMessage }}</span>
    </div>
  </div>
</template>
