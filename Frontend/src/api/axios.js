import axios from 'axios'

const apiClient = axios.create({
  baseURL: 'https://localhost:7280/api/v1',
  timeout: 10000,
  headers: {
    'Content-Type': 'application/json',
  },
})

export default apiClient