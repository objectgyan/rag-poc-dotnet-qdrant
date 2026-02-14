import axios, { AxiosInstance, AxiosError } from 'axios';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5129';
const DEFAULT_API_KEY = import.meta.env.VITE_API_KEY || 'secure_password';

export class ApiClient {
  private client: AxiosInstance;
  private tenantId: string | null = null;
  private apiKey: string;
  private token: string | null = null;

  constructor(apiKey: string = DEFAULT_API_KEY) {
    this.apiKey = apiKey;
    this.client = axios.create({
      baseURL: API_BASE_URL,
      timeout: 120000, // 120 seconds for agent operations (can make multiple Claude API calls)
      headers: {
        'Content-Type': 'application/json',
      },
    });

    this.setupInterceptors();
  }

  setTenantId(tenantId: string | null) {
    this.tenantId = tenantId;
  }

  getTenantId(): string | null {
    return this.tenantId;
  }

  setToken(token: string | null) {
    this.token = token;
  }

  getToken(): string | null {
    return this.token;
  }

  private setupInterceptors() {
    // Request interceptor - add tenant header and auth token if set
    this.client.interceptors.request.use(
      (config) => {
        // Add JWT token if available, otherwise use API key
        if (this.token) {
          config.headers['Authorization'] = `Bearer ${this.token}`;
        } else {
          config.headers['X-API-Key'] = this.apiKey;
        }

        if (this.tenantId) {
          config.headers['X-Tenant-Id'] = this.tenantId;
        }
        console.log(`[API] ${config.method?.toUpperCase()} ${config.url}`, {
          tenantId: this.tenantId,
          hasToken: !!this.token,
        });
        return config;
      },
      (error) => {
        console.error('[API Request Error]', error);
        return Promise.reject(error);
      }
    );

    // Response interceptor - handle errors
    this.client.interceptors.response.use(
      (response) => {
        console.log(`[API Response] ${response.config.url}`, {
          status: response.status,
        });
        return response;
      },
      (error: AxiosError) => {
        if (error.response) {
          console.error('[API Error]', {
            status: error.response.status,
            url: error.config?.url,
            data: error.response.data,
          });

          // Handle specific error codes
          if (error.response.status === 401) {
            console.error('Unauthorized - check API key');
          } else if (error.response.status === 403) {
            console.error('Forbidden - insufficient permissions');
          } else if (error.response.status === 429) {
            console.error('Rate limit exceeded');
          }
        } else if (error.request) {
          console.error('[API Network Error]', error.message);
        }
        return Promise.reject(error);
      }
    );
  }

  // Generic HTTP methods
  async get<T>(url: string, params?: any): Promise<T> {
    const response = await this.client.get<T>(url, { params });
    return response.data;
  }

  async post<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.post<T>(url, data);
    return response.data;
  }

  async put<T>(url: string, data?: any): Promise<T> {
    const response = await this.client.put<T>(url, data);
    return response.data;
  }

  async delete<T>(url: string): Promise<T> {
    const response = await this.client.delete<T>(url);
    return response.data;
  }

  async uploadFile<T>(
    url: string,
    file: File,
    additionalData?: Record<string, string>,
    onProgress?: (progress: number) => void
  ): Promise<T> {
    const formData = new FormData();
    formData.append('file', file);

    if (additionalData) {
      Object.entries(additionalData).forEach(([key, value]) => {
        formData.append(key, value);
      });
    }

    const response = await this.client.post<T>(url, formData, {
      headers: {
        'Content-Type': 'multipart/form-data',
      },
      onUploadProgress: (progressEvent) => {
        if (onProgress && progressEvent.total) {
          const progress = Math.round(
            (progressEvent.loaded * 100) / progressEvent.total
          );
          onProgress(progress);
        }
      },
    });
    return response.data;
  }
}

// Create singleton instance
export const apiClient = new ApiClient();
