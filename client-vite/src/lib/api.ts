import axios from 'axios';

let currentUserId: number = 1; // default; will be updated by UserProvider

export const setApiUserId = (id: number) => {
  currentUserId = id;
};

export const api = axios.create({
  baseURL: '/api',
});

api.interceptors.request.use((config) => {
  config.headers = config.headers ?? {};
  (config.headers as Record<string, string>)['X-User-Id'] = String(currentUserId);
  return config;
});
