import { defineStore } from 'pinia';
import { apiRequest } from '../lib/api';

const STORAGE_KEY = 'library.auth';

function loadStoredAuth() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) || 'null');
  } catch {
    return null;
  }
}

export const useAuthStore = defineStore('auth', {
  state: () => loadStoredAuth() || {
    token: '',
    user: null
  },
  getters: {
    isAuthenticated: (state) => Boolean(state.token),
    role: (state) => state.user?.role || ''
  },
  actions: {
    persist() {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({
        token: this.token,
        user: this.user
      }));
    },
    async login(email, password) {
      const result = await apiRequest('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password })
      });

      this.token = result.accessToken;
      this.user = result;
      this.persist();
      return result;
    },
    async register(fullName, email, password) {
      const result = await apiRequest('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify({ fullName, email, password })
      });

      this.token = result.accessToken;
      this.user = result;
      this.persist();
      return result;
    },
    logout() {
      this.token = '';
      this.user = null;
      localStorage.removeItem(STORAGE_KEY);
    }
  }
});
