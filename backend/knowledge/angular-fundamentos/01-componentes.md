---
slug: angular-componentes
title: Componentes Angular
order: 1
level: Beginner
tags: [component, decorator, selector, template, standalone]
readMinutes: 8
---

## Para que serve

O componente é a unidade básica de construção de qualquer aplicação Angular. Um componente é uma classe TypeScript decorada com `@Component` que controla uma porção de tela chamada de view. Cada componente combina três responsabilidades: a classe define o estado e o comportamento, o template define a estrutura HTML e a folha de estilos define a aparência visual.

## Anatomia de um componente

Todo componente Angular tem três peças obrigatórias: um decorator `@Component`, uma classe TypeScript e um template. O decorator `@Component` marca a classe como um componente Angular. O selector define o nome da tag HTML usada para inserir o componente em outros templates. O template descreve o HTML que será renderizado quando o componente for instanciado.

```ts
import { Component } from '@angular/core';

@Component({
  selector: 'app-saudacao',
  standalone: true,
  template: `
    <h2>Olá, {{ nome }}!</h2>
    <p>Você está conectado há {{ minutos }} minutos.</p>
  `,
  styles: [`
    h2 { color: var(--color-primary); }
  `]
})
export class SaudacaoComponent {
  nome = 'Maria';
  minutos = 12;
}
```

Para usar esse componente em outro template, basta inserir a tag `<app-saudacao></app-saudacao>` no HTML pai. O Angular substitui essa tag pelo template do componente, com os dados ligados pela interpolação `{{ }}`.

## Selector — convenções

O selector deve seguir um padrão kebab-case com prefixo curto, geralmente `app-` para componentes da aplicação. Esse prefixo evita colisões com elementos HTML nativos e com componentes de bibliotecas externas. O Angular CLI gera componentes com prefixo `app-` por padrão, mas é possível trocar essa configuração no `angular.json`.

Selectors também podem usar atributos ou classes CSS, embora o uso de tag seja o mais comum. Por exemplo, `selector: '[app-botao]'` faz o componente ser ativado quando qualquer elemento tem o atributo `app-botao`. Essa forma é útil quando se quer estender comportamento de elementos existentes sem criar uma tag nova.

## Template inline vs templateUrl

O template do componente pode ser definido inline com a propriedade `template` ou em um arquivo separado com a propriedade `templateUrl`. A versão inline é prática para componentes pequenos. Templates externos são preferíveis quando o HTML cresce além de poucas linhas, porque trazem melhor highlight no editor e separam responsabilidades.

```ts
@Component({
  selector: 'app-perfil',
  standalone: true,
  templateUrl: './perfil.component.html',
  styleUrls: ['./perfil.component.scss']
})
export class PerfilComponent {
  usuario = { nome: 'Carlos', email: 'carlos@exemplo.com' };
}
```

A mesma regra vale para estilos: `styles` aceita um array de strings inline e `styleUrls` aponta para arquivos externos. É comum projetos profissionais manterem os três arquivos juntos: `nome.component.ts`, `nome.component.html` e `nome.component.scss`.

## Encapsulamento de estilos

Os estilos definidos em um componente são, por padrão, isolados dele. O Angular aplica um atributo único nos elementos do template e nas regras CSS, garantindo que regras escritas em um componente não vazem para outros. Esse comportamento é controlado pela propriedade `encapsulation` do decorator.

Os três modos disponíveis são `ViewEncapsulation.Emulated` (padrão, baseado em atributos), `ViewEncapsulation.ShadowDom` (usa Shadow DOM real do navegador) e `ViewEncapsulation.None` (sem isolamento, regras se tornam globais). O modo padrão atende quase todos os casos sem necessidade de configuração.

## Componentes Standalone

Desde o Angular 14 e como padrão a partir do Angular 17, componentes podem ser declarados como standalone. Um componente standalone não precisa pertencer a um NgModule e declara suas dependências diretamente na propriedade `imports` do decorator. Esse modelo simplifica a arquitetura e reduz boilerplate.

```ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-tarefa',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <input [(ngModel)]="titulo" placeholder="Nova tarefa" />
    <button (click)="salvar()">Adicionar</button>
    <p *ngIf="titulo">Você digitou: {{ titulo }}</p>
  `
})
export class TarefaComponent {
  titulo = '';
  salvar() {
    console.log('Tarefa:', this.titulo);
  }
}
```

Componentes não-standalone (legacy) precisam ser declarados na propriedade `declarations` de um `@NgModule`, que por sua vez importa outros módulos para disponibilizar diretivas e pipes. O modelo standalone é o recomendado para novos projetos.

## Inputs e Outputs

A comunicação entre componentes pai e filho acontece por meio de inputs e outputs. Inputs são propriedades que o pai passa para o filho usando colchetes na sintaxe do template. Outputs são eventos que o filho emite e o pai escuta usando parênteses.

```ts
import { Component, Input, Output, EventEmitter } from '@angular/core';

@Component({
  selector: 'app-contador',
  standalone: true,
  template: `
    <button (click)="decrementar()">-</button>
    <span>{{ valor }}</span>
    <button (click)="incrementar()">+</button>
  `
})
export class ContadorComponent {
  @Input() valor = 0;
  @Output() mudou = new EventEmitter<number>();

  incrementar() {
    this.valor++;
    this.mudou.emit(this.valor);
  }
  decrementar() {
    this.valor--;
    this.mudou.emit(this.valor);
  }
}
```

No template do pai, o uso fica `<app-contador [valor]="quantidade" (mudou)="atualizar($event)"></app-contador>`. O Angular cuida da propagação dos dados em ambas as direções automaticamente.

## Armadilhas comuns

Um erro frequente é esquecer de marcar o componente como standalone ou de importar suas dependências. Quando uma diretiva como `*ngIf` não funciona em um componente standalone, geralmente é porque o `CommonModule` não foi adicionado ao array `imports`. Outro problema típico é colocar lógica pesada no construtor — a recomendação é usar `ngOnInit` ou os novos hooks baseados em signals para operações de inicialização que dependem de inputs.
