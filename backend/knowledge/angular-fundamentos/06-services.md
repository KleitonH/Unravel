---
slug: angular-services
title: Services e Arquitetura
order: 6
level: Beginner
tags: [service, injectable, architecture, http, singleton]
readMinutes: 7
---

## Para que serve

Service é uma classe Angular que encapsula lógica de negócio, acesso a dados ou qualquer responsabilidade que não pertença a um componente. Services existem para evitar que componentes acumulem múltiplas responsabilidades. Um componente bem desenhado cuida da apresentação e delega lógica para services. Essa separação torna o código mais testável, reutilizável e fácil de manter.

## A classe injetável

Um service é uma classe TypeScript comum decorada com `@Injectable`. O decorator informa ao Angular que essa classe pode ser injetada em outras classes via dependency injection. A propriedade `providedIn` define o escopo de provisão: `'root'` cria um singleton para toda a aplicação, `'platform'` compartilha entre múltiplas aplicações na mesma página, e `'any'` cria uma instância por módulo carregado.

```ts
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class CarrinhoService {
  private itens: { id: number; nome: string; preco: number }[] = [];

  adicionar(item: { id: number; nome: string; preco: number }) {
    this.itens.push(item);
  }

  remover(id: number) {
    this.itens = this.itens.filter(i => i.id !== id);
  }

  listar() {
    return [...this.itens];
  }

  total(): number {
    return this.itens.reduce((soma, i) => soma + i.preco, 0);
  }
}
```

O `providedIn: 'root'` é a configuração padrão para a maioria dos services. Esse escopo garante que toda a aplicação compartilha a mesma instância, o que é desejável para estado global como carrinho, autenticação ou cache.

## Usando services em componentes

Para usar um service em um componente, basta declará-lo no construtor com o tipo da classe. O Angular resolve a instância automaticamente. Desde o Angular 14, também é possível usar a função `inject()` em vez do construtor, o que é especialmente útil em standalone components e em funções utilitárias.

```ts
import { Component, inject } from '@angular/core';
import { CarrinhoService } from './carrinho.service';

@Component({
  selector: 'app-carrinho',
  standalone: true,
  template: `
    <h2>Itens no carrinho: {{ servico.listar().length }}</h2>
    <p>Total: R$ {{ servico.total() }}</p>
    <button (click)="adicionarExemplo()">Adicionar</button>
  `
})
export class CarrinhoComponent {
  servico = inject(CarrinhoService);

  adicionarExemplo() {
    this.servico.adicionar({ id: Date.now(), nome: 'Produto', preco: 99.9 });
  }
}
```

A função `inject()` precisa ser chamada no contexto de inicialização da classe (campos de instância ou construtor). Não pode ser chamada dentro de métodos arbitrários.

## Services para acesso HTTP

Um padrão muito comum é encapsular chamadas HTTP em services. O `HttpClient` do Angular é injetado no service e usado para fazer requisições GET, POST, PUT, DELETE. O service expõe métodos de alto nível que retornam observables, escondendo detalhes da API.

```ts
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

interface Usuario {
  id: number;
  nome: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class UsuarioService {
  private http = inject(HttpClient);
  private base = '/api/usuarios';

  listar(): Observable<Usuario[]> {
    return this.http.get<Usuario[]>(this.base);
  }

  buscar(id: number): Observable<Usuario> {
    return this.http.get<Usuario>(`${this.base}/${id}`);
  }

  criar(dados: Omit<Usuario, 'id'>): Observable<Usuario> {
    return this.http.post<Usuario>(this.base, dados);
  }
}
```

Para que `HttpClient` esteja disponível, é necessário fornecer `provideHttpClient()` no bootstrap da aplicação. Sem essa configuração, a injeção falha em runtime.

## Camadas e responsabilidades

Em aplicações maiores, é comum dividir services em camadas. Services de domínio (como `UsuarioService`) cuidam da lógica de negócio. Services de API encapsulam apenas o transporte HTTP. Services de estado mantêm dados em memória e expõem observables. Services de infraestrutura cuidam de logging, autenticação ou outras responsabilidades transversais.

Essa divisão evita que um único service vire um deus que faz tudo. Quando um service ultrapassa 200 linhas, vale considerar dividir suas responsabilidades. Cada service deve ter uma razão única para mudar.

## Services com estado e signals

Antigamente, services compartilhavam estado entre componentes usando `BehaviorSubject` do RxJS. Com a chegada dos signals no Angular 16, surgiu uma alternativa mais simples e síncrona. Um service pode expor signals públicos que os componentes leem reativamente.

```ts
import { Injectable, signal, computed } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class TemaService {
  private modo = signal<'claro' | 'escuro'>('claro');

  modoAtual = this.modo.asReadonly();
  estaEscuro = computed(() => this.modo() === 'escuro');

  alternar() {
    this.modo.update(m => m === 'claro' ? 'escuro' : 'claro');
  }
}
```

No componente, basta ler o signal: `<div [class.dark]="tema.estaEscuro()">`. O Angular detecta automaticamente a dependência e atualiza a view quando o signal muda.

## Testando services

Services são fáceis de testar porque são apenas classes TypeScript. Para testes unitários puros, basta instanciar a classe e chamar métodos. Para testes que envolvem dependências injetadas, usa-se o `TestBed` do `@angular/core/testing` para criar um injector de teste e fornecer mocks.

```ts
import { TestBed } from '@angular/core/testing';
import { CarrinhoService } from './carrinho.service';

describe('CarrinhoService', () => {
  let servico: CarrinhoService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    servico = TestBed.inject(CarrinhoService);
  });

  it('soma corretamente o total', () => {
    servico.adicionar({ id: 1, nome: 'X', preco: 10 });
    servico.adicionar({ id: 2, nome: 'Y', preco: 20 });
    expect(servico.total()).toBe(30);
  });
});
```

## Armadilhas comuns

Confundir o escopo de provisão é um erro frequente. Um service `providedIn: 'root'` é singleton; se você cria estado nele e espera múltiplas instâncias, vai obter comportamento incorreto. Outro erro é injetar componentes em services — a direção deve ser sempre componente depende de service, nunca o inverso. Por fim, evite criar instâncias manuais com `new MeuService()`; isso quebra a dependency injection e dificulta testes.
